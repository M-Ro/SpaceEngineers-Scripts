const string SCRIPTNAME = "SmartRefineries";
const string VERSION = "1.6.3";

// Refinery group name and Ore container name
const string GROUP_REFINERIES = "Refineries";
const string BLOCK_ORESTORAGE = "OreStorage";

// LCD Names
const string LCD_ORE = "lcdOre";
const string LCD_REFINERY = "lcdRefinery";

// LCD Display Colors
Color COLOR_REFINERY = new Color(30, 255, 30);
Color COLOR_ORE = new Color(222, 184, 135);

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

// Lists of prioritised and blacklisted ores
List<string> priorityList = new List<string>();
List<string> blacklist = new List<string>();

// Hack because tuples aren't allowed
internal class IntPair
{
    public int first;
    public int second;

    public IntPair(int first, int second) { this.first = first; this.second = second; }
};
 
int oreCount = 0; // Updated from StackContainer

void Main(string argument)  
{
    Echo(SCRIPTNAME + " " + VERSION);
    switch(argument)
    {
        case "DisplayMenuUp": LCDMenuUp(); HandleLCDPanel("Display"); return;
        case "DisplayMenuDown": LCDMenuDown(); HandleLCDPanel("Display"); return;
        case "Toggle": HandleMenuToggle(); return;
        case "Prioritise": HandleMenuPrioritise(); return;
        default: break;
    };

    List<IMyTerminalBlock> refineries = GetTypedBlockGroup<IMyRefinery>(GROUP_REFINERIES);
    IMyCargoContainer oreStore = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE);

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
            FillRefinery((IMyRefinery)refineries[i], oreStore, oreIndex);
    }
}

int curOre = 0;
int GetNextOre()
{
    IMyCargoContainer c = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE);
    IMyInventory cInv = c.GetInventory(0);
    List<IMyInventoryItem> items = cInv.GetItems();

    if(priorityList.Count > 0) // Priority mode
    {
        ValidatePriorityList();
        if(priorityList.Count <= 0)
            return GetNextOre();

        curOre = curOre % priorityList.Count; // Safety if switching to prioritise....

        string oreType = priorityList[curOre];
        for(int i=0; i<oreCount; i++)
        {
            int index = (curOre+i) % oreCount;
            
            if(!cInv.IsItemAt(index)) // Item isn't there anymore
                continue;
                
            IMyInventoryItem ore = items[index];
            if(IsOre(ore) && ore.Content.SubtypeId.ToString() == oreType)
                return index;
        }

        curOre++;
    }
    else // Check blacklist
    {
        for(int i=0; i<oreCount; i++)
        {
            int oreIndex = (curOre+i) % oreCount;
            
            if(!cInv.IsItemAt(oreIndex)) // Item isn't there anymore
                continue;
            
            IMyInventoryItem ore = items[oreIndex];

            if(IsOre(ore) && !blacklist.Contains(ore.Content.SubtypeId.ToString()))
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
    IMyCargoContainer c = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE);
    IMyInventory cInv = c.GetInventory(0);
    List<IMyInventoryItem> items = cInv.GetItems();

    for(int i=0; i<priorityList.Count; i++)
    {
        string oreType = priorityList[i];
        bool found = false; 
        for(int j=0; j<oreCount; j++) 
        {
            if(!cInv.IsItemAt(j))
                break;
            
            if(IsOre(items[j]) && items[j].Content.SubtypeId.ToString() == oreType) 
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
        if(IsOre(item))
        {
            string oretype = item.Content.SubtypeId.ToString();
            
            if(oresStacked.Contains(oretype))
                continue;

            for(int j=i+1; j<items.Count; j++)
                if(IsOre(item) && items[j].Content.SubtypeId.ToString() == oretype)
                    mergeStacks.Add(new IntPair(i, j));

            oresStacked.Add(oretype);
        }
        else // We push non-ores to the back of the queue
            mergeStacks.Add(new IntPair(items.Count+90000, i)); // Hack to push non-ores to back of stack...
    }

    // Perform the list of inventory merges we created
	for(int j=0; j<mergeStacks.Count; j++)
    {
		IntPair merge = mergeStacks[j];

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
    if(!oreInventory.IsItemAt(oreNum) || !IsOre(oreItems[oreNum]))
        return;

    // Get how much the refinery currently has
    int currentAmount=0;
    if(refineryInventory.IsItemAt(0))
        currentAmount = (int)refineryInventory.GetItems()[0].Amount;
    
    // Calculate amount to deposit
	int depositAmount = 500; // Default
	if(oreDepositPerTick.ContainsKey(oreItems[oreNum].Content.SubtypeId.ToString()))
		depositAmount = oreDepositPerTick[oreItems[oreNum].Content.SubtypeId.ToString()] - currentAmount;
    
    if(depositAmount > 0)
        oreInventory.TransferItemTo(refinery.GetInventory(0), oreNum, 0, true, depositAmount);
}

/* Disables/Enables refinery at index. Empties ore if disabling */
void ToggleRefinery(int index)
{
    List<IMyTerminalBlock> refineries = GetTypedBlockGroup<IMyRefinery>(GROUP_REFINERIES);
   
    // Safety check
    if(index >= refineries.Count)
        return;

    IMyRefinery r = (IMyRefinery)refineries[index];

    /* If the refinery was enabled, empty ore from it */
    if(r.IsWorking == true)
    {
        IMyCargoContainer container = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE);
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

/* Returns true if item qualifies as MyObjectBuilder_Ore AND SubtypeID is NOT ice */
bool IsOre(IMyInventoryItem item)
{
    return item.Content.TypeId.ToString() == "MyObjectBuilder_Ore" && item.Content.SubtypeId.ToString() != "Ice";
}

/* Returns the first type-validated block with the given name
 * Usage: IMyType block = GetTypedBlock<IMyType>(blockname) */
T GetTypedBlock<T>(string targetname) where T : class, IMyTerminalBlock
{
    List<IMyTerminalBlock> search = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.SearchBlocksOfName(targetname, search);
    
	for(int i=0; i<search.Count; i++)
        if(search[i] is T)
            return (T)search[i];
        
    return null;
}

/* Returns a type validated list of Terminal Blocks.
 * Usage: List<IMyType> lights = GetTypedBlockGroup<IMyType>(groupname) */
/* NOTE: List<T> would be preferable, but does not work due to SE scripting limitations */
List<IMyTerminalBlock> GetTypedBlockGroup<T>(string groupname)
{   
    IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(groupname);
    if(group != null && group.Blocks.Count > 0)
    {
        List<IMyTerminalBlock> typed_blocks = new List<IMyTerminalBlock>();

		for(int i=0; i<group.Blocks.Count; i++)
            if(group.Blocks[i] is T)
                typed_blocks.Add(group.Blocks[i]);
        
        return typed_blocks;

    }

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
    IMyTextPanel lcd_ore = FindLCDPanel(LCD_ORE);
    IMyTextPanel lcd_refinery = FindLCDPanel(LCD_REFINERY);

    if(lcd_ore != null)
        RenderOreDisplay(lcd_ore);

    if(lcd_refinery != null)
        RenderRefineryDisplay(lcd_refinery);
}

void RenderOreDisplay(IMyTextPanel panel) // TODO share IMyCargoContainer reference instead of searching
{
    panel.SetValue("FontColor", COLOR_ORE);
    string output = LCDTitle("Ore Status", false) + "\n\n";
    
    IMyCargoContainer oreContainer = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE);
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
    panel.SetValue("FontColor", COLOR_REFINERY); 
    string output = LCDTitle("Refineries", true) +"\n\n";

    List<IMyTerminalBlock> refineries = GetTypedBlockGroup<IMyRefinery>(GROUP_REFINERIES);

    maxSelectionRefinery=0;
    int totalPowerUsage = 0;
    for(int i=0; i<refineries.Count; i++)
    {
        IMyRefinery r = (IMyRefinery)refineries[i];
        String refineryInfo = "        ";
		
		String refineryName = r.CustomName;
		if(refineryName.Length > 11)
		{
			refineryName = refineryName.Substring(0, 11);
			refineryName += "..";
		}
		
        refineryInfo += PadRightToPixels(refineryName, 350) + "-- ";
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
        IMyCargoContainer c = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE);
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
        List<IMyTerminalBlock> refineries = GetTypedBlockGroup<IMyRefinery>(GROUP_REFINERIES);
        IMyCargoContainer c = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE);
        if(refineries != null)
        {
			for(int i=0; i<refineries.Count; i++)
            {
				IMyRefinery refinery = (IMyRefinery)refineries[i];
				
                refinery.GetActionWithName(
                    (menuSelection == maxSelectionOre+maxSelectionRefinery-2) ? "OnOff_Off" : "OnOff_On").Apply(refinery);
                
                if(menuSelection == maxSelectionOre+maxSelectionRefinery-2) // Empty if disabling
                    EmptyRefinery(refinery, c);
            }
        }
    }

    IMyTextPanel lcd_ore = FindLCDPanel(LCD_ORE); 
    IMyTextPanel lcd_refinery = FindLCDPanel(LCD_REFINERY);

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
        IMyCargoContainer c = GetTypedBlock<IMyCargoContainer>(BLOCK_ORESTORAGE); 
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


    IMyTextPanel lcd_ore = FindLCDPanel(LCD_ORE);  
    IMyTextPanel lcd_refinery = FindLCDPanel(LCD_REFINERY); 
 
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
	for(int i=0; i<s.Length; i++)
        len += CharWidth(s[i]);

    return len;
}

string PadRightToPixels(string s, int px)
{
    int curWidth = StringWidth(s);
    int numSpaces = (int)Math.Round((float)(px - curWidth) / (float)CharWidth(' '));
    //int numSpaces = (int)Math.Ceiling((float)(px - curWidth) / (float)CharWidth(' ')); // ?? Works better ??

	if(numSpaces > 0)
		s = s.PadRight(s.Length + numSpaces, ' ');
	
	return s;
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
