/* -- Configuration Variables -- */
const string GROUP_LIGHTS = "LightsGroup";  // Groupname of lights
const int 	 DURATION 	  = 250; 			// Pulse duration in milliseconds
/* ----------------------------- */


long last_timestamp = 0; // timestamp of last pulse
int cur_light = 0; 		 // Current light in sequence.

void Main(string argument)
{
	if(GetTime() > last_timestamp + DURATION)
	{
		last_timestamp = GetTime();
		
		List<IMyTerminalBlock> lights = GetTypedBlockGroup<IMyTerminalBlock>(GROUP_LIGHTS);
		if(lights == null)
			return;
		
		// Sort lights by name alphanumerically
		lights.Sort((l1, l2) => l1.CustomName.CompareTo(l2.CustomName));
		
		cur_light = cur_light % lights.Count;
		
		// Enable current light. Disable others
		for(int i=0; i<lights.Count; i++)
			lights[i].GetActionWithName(i != cur_light ? "OnOff_Off" : "OnOff_On").Apply(lights[i]);
		
		cur_light = (cur_light + 1) % lights.Count;
	}
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

/* Returns current time in milliseconds */
long GetTime()
{
	return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
}