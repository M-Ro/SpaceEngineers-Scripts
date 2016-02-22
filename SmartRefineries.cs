const string GROUP_REFINERIES = "Refineries";
const string ORE_STORAGE_GROUP = "OreStorage";

// Maximum amount of ore each refinery will have per tick
Dictionary <string, int> oreDepositPerTick = new Dictionary<string, int>()
{
	{"Scrap", 500},
	{"Iron", 500},
	{"Stone", 300},
	{"Gold", 80},
	{"Silicon", 50},
	{"Magnesium", 30},
	{"Silver", 30},
	{"Nickel", 20},
	{"Platinum", 8},
	{"Uranium", 8},
	{"Cobalt", 8} 
};

List<string> priorityList = new List<string>();
List<string> blacklist = new List<string>();

internal class IntPair // Hack because tuples aren't allowed
{
	public int first;
	public int second;

	public IntPair(int first, int second) { this.first = first; this.second = second; }
};

 
int oreCount = 0; // Updated from StackContainer

void Main(string argument)  
{
	Echo("SmartRefineries Version 1.5");
	switch(argument)
	{
		case "DisplayMenuUp": LCDMenuUp(); HandleLCDPanel("Display"); return;
		case "DisplayMenuDown": LCDMenuDown(); HandleLCDPanel("Display"); return;
		case "Toggle": HandleMenuToggle(); return;
		case "Prioritise": HandleMenuPrioritise(); return;
		default: break;
	};

	List<IMyRefinery> refineries = GetRefineries(GROUP_REFINERIES);
	IMyCargoContainer oreStore = GetOreContainer(ORE_STORAGE_GROUP);

	// Safety check
	if(refineries == null || oreStore == null || refineries.Count == 0)
		return;

	curOre = 0;
	StackInventory(oreStore);
	HandleLCDPanel("Display");
	if(oreCount <= 0)
		return;

	refineries.RemoveAll(r => !r.IsWorking); // Remove disabled refineries from list

	for(int i=0; i<refineries.Count; i++)
	{
		int oreIndex = GetNextOre();
		 if(oreIndex >= 0)
			FillRefinery(refineries[i], oreStore, oreIndex);
	}
}

/* Getter functions for our blocks, this would be FAR better as a generic 
 *  but Space Engineers doesn't allow Types, generics, templates.... */  
List<IMyRefinery> GetRefineries(string groupname)  
{  
	IMyBlockGroup targetGroup = GetGroup(groupname); 
	if(targetGroup == null) return null; 
 
	List<IMyRefinery> targets = new List<IMyRefinery>();  
 
	foreach(IMyTerminalBlock b in targetGroup.Blocks)  
		if(b is IMyRefinery)   
			targets.Add((IMyRefinery)b);  
  
	return targets;  
}

int curOre = 0;
int GetNextOre()
{
	IMyCargoContainer c = GetOreContainer(ORE_STORAGE_GROUP);
	IMyInventory cInv = c.GetInventory(0);
	List<IMyInventoryItem> items = cInv.GetItems();


	if(priorityList.Count > 0) // Priority mode
	{
			ValidatePriorityList();
			if(priorityList.Count <= 0)
				return GetNextOre();

			int count = priorityList.Count;
			curOre = curOre % count; // Safety if switching to prioritise....

			int index = 0;
			string oreType = priorityList[curOre];
			for(int i=0; i<oreCount; i++)
			{
				IMyInventoryItem ore = items[(curOre+i) % oreCount];
				if(ore.Content.SubtypeId.ToString() == oreType)
				{
					index = (curOre+i) % oreCount;
					break;
				}
			}

			curOre++;
			return index;
	}
	else // Check blacklist
	{
		for(int i=0; i<oreCount; i++)
		{
			int oreIndex = (curOre+i) % oreCount;
			IMyInventoryItem ore = items[oreIndex];

			if(!blacklist.Contains(ore.Content.SubtypeId.ToString()))
			{
				curOre = oreIndex+1;
				return oreIndex;
			}
		}
	}

	return -1;
}

// Removes priority flag on ores we no longer have...
void ValidatePriorityList()
{
	IMyCargoContainer c = GetOreContainer(ORE_STORAGE_GROUP);
	IMyInventory cInv = c.GetInventory(0);
	List<IMyInventoryItem> items = cInv.GetItems();

	for(int i=0; i<priorityList.Count; i++)
	{
		string oreType = priorityList[i];
		bool found = false; 
		for(int j=0; j<oreCount; j++) 
		{ 
			if(items[j].Content.SubtypeId.ToString() == oreType) 
			{ 
				found = true; 
				break; 
			} 
		} 
 
		if(!found)
		{
			priorityList.Remove(oreType);
			i--;
		}
	}
}

// Stacks and merged all ores, pushes non-ores to end of inventory
void StackInventory(IMyCargoContainer container)
{
	IMyInventory inv = container.GetInventory(0);
	List<IMyInventoryItem> items = inv.GetItems();
	List<IntPair> mergeStacks = new List<IntPair>();
	List<string> oresStacked = new List<string>();

	for(int i=0; i<items.Count; i++)
	{
		IMyInventoryItem item = items[i];
		if(item.Content.TypeId.ToString() == "MyObjectBuilder_Ore" && item.Content.SubtypeId.ToString() != "Ice")
		{
			string oretype = item.Content.SubtypeId.ToString();
			
			if(oresStacked.Contains(oretype))
				continue;

			for(int j=i+1; j<items.Count; j++)
				if(items[j].Content.TypeId.ToString() == "MyObjectBuilder_Ore" && items[j].Content.SubtypeId.ToString() == oretype)
					mergeStacks.Add(new IntPair(i, j));

			oresStacked.Add(oretype);
		}
		else // We push non-ores to the back of the queue
			mergeStacks.Add(new IntPair(items.Count+90000, i)); // Hack to push non-ores to back of stack...
	}

	// Perform the list of inventory merges we created
	foreach(IntPair merge in mergeStacks)
	{
		for(int i=0; i<mergeStacks.Count; i++)
		{
			if(mergeStacks[i].first > merge.second) (mergeStacks[i]).first--;
			if(mergeStacks[i].second > merge.second) mergeStacks[i].second--;
		}

		inv.TransferItemTo(inv, merge.second, merge.first, null);
	}

	oreCount = oresStacked.Count;
}

/* Fills the given refinery with the ore index from the given cargo store */
void FillRefinery(IMyRefinery refinery, IMyCargoContainer oreStore, int oreNum)
{
	IMyInventory oreInventory = oreStore.GetInventory(0);
	List<IMyInventoryItem> oreItems = oreInventory.GetItems();
	
	IMyInventory refineryInventory = refinery.GetInventory(0);
	
	// Validation check...
	if(!oreInventory.IsItemAt(oreNum) ||oreItems[oreNum].Content.TypeId.ToString() != "MyObjectBuilder_Ore")
		return;

	// Get how much the refinery currently has
	int currentAmount=0;
	if(refineryInventory.IsItemAt(0))
		currentAmount = (int)refineryInventory.GetItems()[0].Amount;
	
	// Calculate amount to deposit
	int depositAmount = oreDepositPerTick[oreItems[oreNum].Content.SubtypeId.ToString()] - currentAmount;
	
	if(depositAmount > 0)
		oreInventory.TransferItemTo(refinery.GetInventory(0), oreNum, 0, true, depositAmount);
}

/* Disables/Enables refinery at index. Empties ore if disabling */
void ToggleRefinery(int index)
{
	List<IMyRefinery> refineries = GetRefineries(GROUP_REFINERIES);
   
	// Safety check
	if(index >= refineries.Count)
		return;

	IMyRefinery r = refineries[index];

	/* If the refinery was enabled, empty ore from it */
	if(r.IsWorking == true)
	{
		IMyCargoContainer container = GetOreContainer(ORE_STORAGE_GROUP);
		if(container != null)
			EmptyRefinery(r, container);
	}

	r.GetActionWithName(r.IsWorking ? "OnOff_Off" : "OnOff_On").Apply(r);
}

/* Removes all items from the ore inventory of a refinery */
void EmptyRefinery(IMyRefinery r, IMyCargoContainer c)
{
	IMyInventory oreContainer = c.GetInventory(0); 
	IMyInventory inv = r.GetInventory(0); 

	/* We reverse iterate here, avoiding the game's auto-stacking
	 * interfering and moving inventory indexes while we are working */
	for(int i=inv.GetItems().Count-1; i>=0; i--) 
		inv.TransferItemTo(oreContainer,  i, null, true);
	
	// Official documentation method. Does not work.
	//for (int i=0; i < inv.GetItems().Count; i++)
		//inv.TransferItemTo(oreContainer, 0, null, true, null);
}

IMyCargoContainer GetOreContainer(string groupname) 
{ 
	IMyBlockGroup targetGroup = GetGroup(groupname);
	if(targetGroup == null) return null;

	foreach(IMyTerminalBlock b in targetGroup.Blocks) 
		if(b is IMyCargoContainer)  
			return (IMyCargoContainer)b;
 
	return null; 
}

IMyBlockGroup GetGroup(string groupname)
{
	List<IMyBlockGroup> groups = new List<IMyBlockGroup>();  
	GridTerminalSystem.GetBlockGroups(groups); 
  
	foreach(IMyBlockGroup g in groups)  
		if(g.Name == groupname)
			return g;

	return null;
}

/* ======= LCD / CONTROL PANEL FUNCTIONS ======= */

int menuSelection = 0;
int maxSelectionOre = 0;
int maxSelectionRefinery = 0;

void HandleLCDPanel(string argument) // Entrypoint
{
	switch(argument)
	{
		case "Display": LCDDisplay(); break;
		default: break;
	};
}

void LCDDisplay()
{
	IMyTextPanel lcd_ore = FindLCDPanel("LCDOreCtrl");
	IMyTextPanel lcd_refinery = FindLCDPanel("LCDRefineryCtrl");

	if(lcd_ore != null)
		RenderOreDisplay(lcd_ore);

	if(lcd_refinery != null)
		RenderRefineryDisplay(lcd_refinery);
}

void RenderOreDisplay(IMyTextPanel panel) // TODO share IMyCargoContainer reference instead of searching
{
	panel.SetValue("FontColor", new Color(222, 184, 135));
	string output = LCDTitle("Ore Status", false) + "\n\n";
	
	IMyCargoContainer oreContainer = GetOreContainer(ORE_STORAGE_GROUP);
	IMyInventory oreInventory = null;
	if(oreContainer != null)
		oreInventory = oreContainer.GetInventory(0);
	
	List<IMyInventoryItem> items = oreInventory.GetItems();

	maxSelectionOre = 0;
	int i = 0;
	while(i < items.Count && i < oreCount)
	{
		String oreInfo =  "        ";
		oreInfo += PadRightToPixels(items[i].Content.SubtypeName, 300) + ((float)(items[i].Amount)).ToString("0.00") + "kg";
		if(blacklist.Contains(items[i].Content.SubtypeId.ToString()))
			oreInfo += "    X";
		if(priorityList.Contains(items[i].Content.SubtypeId.ToString())) 
			oreInfo += "    !!!";
		oreInfo += "\n";
		if(i == menuSelection)
			oreInfo = oreInfo.Remove(1, 4).Insert(1, ">>");
  
		output += oreInfo;
		maxSelectionOre++;

		i++;
	}

	output = LCDFooter(output);

	panel.WritePublicText(output);
	panel.ShowTextureOnScreen();
	panel.ShowPublicTextOnScreen();
}

void RenderRefineryDisplay(IMyTextPanel panel)
{
	panel.SetValue("FontColor", new Color(30, 255, 30)); 
	string output = LCDTitle("Refineries", true) +"\n\n";

	List<IMyRefinery> refineries = GetRefineries(GROUP_REFINERIES);

	maxSelectionRefinery=0;
	int totalPowerUsage = 0;
	for(int i=0; i<refineries.Count; i++)
	{
		IMyRefinery r = refineries[i];
		String refineryInfo = "        ";
		refineryInfo += PadRightToPixels(r.CustomName, 350) + "-- ";
		refineryInfo += PadRightToPixels(((r.IsWorking) ? "On " : "Off"), 115) + "--    ";
		if(r.GetInventory(0).GetItems().Count > 0)
			refineryInfo += r.GetInventory(0).GetItems()[0].Content.SubtypeId;
		else
			refineryInfo += "Empty";
		refineryInfo += "\n";
		if(menuSelection >= maxSelectionOre && i == menuSelection-maxSelectionOre) 
			refineryInfo = refineryInfo.Remove(1, 4).Insert(1, ">>");

		output += refineryInfo;
		maxSelectionRefinery++;

		// Add refinery power usage
		totalPowerUsage += ConvertStringWattageToInt(r.DetailedInfo.Split('\n')[2]);
	}

	output += "\n";
	output += menuSelection == maxSelectionOre + maxSelectionRefinery ? " >>     [Disable All]\n" : "          [Disable All]\n";
	output += menuSelection == maxSelectionOre + maxSelectionRefinery+1 ? " >>     [Enable All]\n" : "          [Enable All]\n"; 
	maxSelectionRefinery += 2;

	output += "\n" + LCDHorizontalLine('-')  + "\n";
	output += "Power Consmption: " + FormatToWattage(totalPowerUsage) + "\n";
 
	output = LCDFooter(output);
	panel.WritePublicText(output);

	panel.ShowTextureOnScreen(); 
	panel.ShowPublicTextOnScreen();
}

void LCDMenuUp()
{
	menuSelection--;
	if(menuSelection < 0) menuSelection = maxSelectionOre + maxSelectionRefinery-1;
}

void LCDMenuDown()
{
	menuSelection++;
	if(menuSelection >= maxSelectionOre + maxSelectionRefinery) menuSelection = 0;
}

void HandleMenuToggle()
{
	if(menuSelection < maxSelectionOre) // Blacklist/Whitelist ore
	{
		int oreIndex = menuSelection;
		IMyCargoContainer c = GetOreContainer(ORE_STORAGE_GROUP);
		if(c == null)
			return;
		IMyInventory cInv = c.GetInventory(0);
		List<IMyInventoryItem> items = cInv.GetItems();

		if(priorityList.Contains(items[menuSelection].Content.SubtypeId.ToString()))
			priorityList.Remove(items[menuSelection].Content.SubtypeId.ToString());

		if(blacklist.Contains(items[menuSelection].Content.SubtypeId.ToString()))
			blacklist.Remove(items[menuSelection].Content.SubtypeId.ToString());
		else
			blacklist.Add(items[menuSelection].Content.SubtypeId.ToString());
	}
	else if(menuSelection < maxSelectionOre + maxSelectionRefinery-2)       // Toggle Refinery
	{
		int refineryIndex = menuSelection - maxSelectionOre;
		ToggleRefinery(refineryIndex);
	}
	else    // Enable All / Disable all
	{
		List<IMyRefinery> refineries = GetRefineries(GROUP_REFINERIES);
		IMyCargoContainer c = GetOreContainer(ORE_STORAGE_GROUP);
		if(refineries != null)
		{
			foreach(IMyRefinery refinery in refineries)
			{
				refinery.GetActionWithName(
					(menuSelection == maxSelectionOre+maxSelectionRefinery-2) ? "OnOff_Off" : "OnOff_On").Apply(refinery);
				
				if(menuSelection == maxSelectionOre+maxSelectionRefinery-2) // Empty if disabling
					EmptyRefinery(refinery, c);
			}
		}
	}

	IMyTextPanel lcd_ore = FindLCDPanel("LCDOreCtrl"); 
	IMyTextPanel lcd_refinery = FindLCDPanel("LCDRefineryCtrl");

	if(lcd_ore != null) 
		RenderOreDisplay(lcd_ore); 
 
	if(lcd_refinery != null) 
		RenderRefineryDisplay(lcd_refinery);
}

void HandleMenuPrioritise()
{
	if(menuSelection < maxSelectionOre) // (De)prioritise ore
	{ 
		int oreIndex = menuSelection; 
		IMyCargoContainer c = GetOreContainer(ORE_STORAGE_GROUP); 
		if(c == null) 
			return; 
		IMyInventory cInv = c.GetInventory(0); 
		List<IMyInventoryItem> items = cInv.GetItems();

		// Can't prioritise blacklisted ores
		if(blacklist.Contains(items[menuSelection].Content.SubtypeId.ToString()))
			return;

		if(!priorityList.Contains(items[menuSelection].Content.SubtypeId.ToString()))
			priorityList.Add(items[menuSelection].Content.SubtypeId.ToString());
		else
			priorityList.Remove(items[menuSelection].Content.SubtypeId.ToString());
	}


	IMyTextPanel lcd_ore = FindLCDPanel("LCDOreCtrl");  
	IMyTextPanel lcd_refinery = FindLCDPanel("LCDRefineryCtrl"); 
 
	if(lcd_ore != null)  
		RenderOreDisplay(lcd_ore);  
  
	if(lcd_refinery != null)  
		RenderRefineryDisplay(lcd_refinery);
}

/* Returns the requested LCD panel (by name) from the Grid system */
IMyTextPanel FindLCDPanel(string target)
{
	List<IMyTerminalBlock> lcd_search = new List<IMyTerminalBlock>(); 
	GridTerminalSystem.SearchBlocksOfName(target, lcd_search);
	
	for(int i=0; i<lcd_search.Count; i++)
		if(lcd_search[i] is IMyTextPanel)
			return (IMyTextPanel)(lcd_search[i]);

	return null;
}

/* Handy printing functions */
const int LCD_COLUMNS = 60;
const int LCD_ROWS = 18;
const char SEPARATOR = '=';

/* Returns a horizontal line */
string LCDHorizontalLine(char? separator = SEPARATOR)
{
	String str = "";
	str = str.PadLeft(LCD_COLUMNS, (char)separator);
	return str;
}

/* Returns centered input padded with horizontal line */
string LCDTitle(string title, bool drawTime)
{
	title = title.PadLeft(title.Length+1, ' ').PadRight(title.Length+2, ' '); // Spaces on either side
	
	/* Pad with - */
	int free_cols = LCD_COLUMNS - title.Length;
	title = title.PadLeft(title.Length + free_cols/4, '=');
	title = title.PadRight(title.Length + free_cols/4, '=');
	title += '=';

	/* Get system time */
	if(drawTime)
	{
		String time = " " + DateTime.Now.ToString("HH:mm") + " ";
		title = title.Insert(title.Length-5, time);
	}
  
	return title;
}

string LCDFooter(string input)
{
	int curLine = input.Split('\n').Length-1;
	while(curLine < LCD_ROWS-1)
	{
		input += "\n";
		curLine++;
	}

	 input += "".PadLeft(LCD_COLUMNS, SEPARATOR);
	 return input;
}

/* ==== Auxiliary helper functions ==== */
public int Clamp(int val, int min, int max)
{
	return (val < min) ? min : (val > max) ? max : val;
}

const string multipliers = "kMGT"; // kilo, Mega, Giga....
/* Converts watt decimal to simplified string
* ie 67222W =>   67.22 kW */
string FormatToWattage(int wattage)
{
	float w = wattage;
	int m = -1;
	while(w >= 1000f)
	{
		w /= 1000f;
		m++;
	}

	string str = w + " " + ((m >= 0) ? ""+ multipliers[m] : "") + "W";
	return str;
}

/* Converts "wattage" value (e.g 16.02 kW) to numerical watts */
int ConvertStringWattageToInt(string wattage)
{
	float w = Convert.ToSingle(System.Text.RegularExpressions.Regex.Match(wattage, "\\d+\\.?\\d*").Value);
	char multiplier = wattage[wattage.Length-2];

	switch(multiplier) 
	{ 
		case 'k': w *= 1000; break; 
		case 'M': w *= 1000*1000; break;
		case 'G': w *= 1000*1000*1000; break;
	}

	return (int)w;
}

/* Dirty Variable width font workarounds */
int CharWidth(char c)
{
	return charWidth[c-32];
}

int StringWidth(string s)
{
	int len = 0;
	foreach(char c in s)
		len += CharWidth(c);

	return len;
}

string PadRightToPixels(string s, int px)
{
	int curWidth = StringWidth(s);
	int numSpaces = (int)Math.Round((float)(px - curWidth) / (float)CharWidth(' '));
	//int numSpaces = (int)Math.Ceiling((float)(px - curWidth) / (float)CharWidth(' ')); // ?? Works better ??

	return s.PadRight(s.Length + numSpaces, ' ');
}

/* This should be a static array, but apparently
	* we aren't allowed those.... */
List<int> charWidth = new List<int>()
{
	15, 24, 25, 35, 36, 39, 35, 22, 24, 
	24, 26, 34, 25, 25, 25, 30, 35, 24, 
	34, 33, 34, 35, 35, 31, 35, 35, 25, 
	25, 34, 34, 34, 31, 40, 37, 37, 35, 
	37, 34, 32, 36, 35, 24, 31, 33, 30, 
	42, 37, 37, 35, 37, 37, 37, 32, 36, 
	35, 47, 35, 36, 35, 25, 28, 25, 34, 
	31, 23, 33, 33, 32, 33, 33, 24, 33, 
	33, 23, 23, 32, 23, 42, 33, 33, 33, 
	33, 25, 33, 25, 33, 30, 42, 31, 33, 
	31, 25, 22, 25, 34
};
