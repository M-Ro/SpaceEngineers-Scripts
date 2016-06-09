const string BLOCK_SENSOR   = "ssrAirlock";
const string BLOCK_VENT 	= "Airlock Vent";
const string GROUP_DOORS 	= "Airlock Doors";
const string GROUP_DOORS_EXTERIOR = "Airlock Exterior";
const string GROUP_DOORS_INTERIOR = "Airlock Interior";

const int TIMEOUT = 25000; // Timeout(ms) before resetting state if player doesn't enter airlock


bool locked = false;

/* 0: Nothing
 * 1: out->middle
 * 2: in->middle
 * 3: middle->out
 * 4: middle->in */
int state = 0;

long timestampTriggered = 0;

// Constructor
public Program()
{
	Load();
	Reset();
}

public void Load()
{

}

// Storage field
public void Save() 
{

}


public void Main(string argument)
{

    switch(argument)
	{
		case "lock": HandleLock(); break;
		case "unlock": HandleUnlock(); break;
		case "toggleLock": if(locked) {HandleUnlock();} else {HandleLock();} break;
		case "handleButtonExterior": HandleButtonExterior(); break;
		case "handleButtonInterior": HandleButtonInterior(); break;
	}
	
	if(state > 0)
		Process();
	else
		CheckTrappedPlayer();
}

// Check if a player is somehow stuck inside the system
private void CheckTrappedPlayer()
{
	IMySensorBlock sensor = GetTypedBlock<IMySensorBlock>(BLOCK_SENSOR);
	if(state == 0 && !locked && sensor.IsActive)
		state = 4; // Let them in
}

private void Process()
{
	IMyAirVent vent = GetTypedBlock<IMyAirVent>(BLOCK_VENT);
	IMySensorBlock sensor = GetTypedBlock<IMySensorBlock>(BLOCK_SENSOR);
	List<IMyTerminalBlock> doorsExterior = GetTypedBlockGroup<IMyDoor>(GROUP_DOORS_EXTERIOR);
	List<IMyTerminalBlock> doorsInterior = GetTypedBlockGroup<IMyDoor>(GROUP_DOORS_INTERIOR);
	
	
	if(vent == null || sensor == null)
		return;
	
	float oxygenLevel = vent.GetOxygenLevel(); // 0.0 -> 1.0
	
	if(state == 1) // out -> middle
	{
		if(oxygenLevel > 0)
			return;
		else if(oxygenLevel <= 0 && sensor.IsActive) // Next stage...
		{
			for(int i=0; i<doorsExterior.Count; i++)
				doorsExterior[i].GetActionWithName("Open_Off").Apply(doorsExterior[i]);
			
			vent.GetActionWithName("Depressurize_Off").Apply(vent);
			
			state = 4;
		}
		else if(oxygenLevel <= 0)
		{
			for(int i=0; i<doorsExterior.Count; i++)
				doorsExterior[i].GetActionWithName("Open_On").Apply(doorsExterior[i]);
		}
	}
	else if(state == 2) // in -> middle
	{
		if(oxygenLevel < 0.98)
			return;
		else if(oxygenLevel >= 0.98 && sensor.IsActive) // Next stage...
		{
			for(int i=0; i<doorsInterior.Count; i++)
				doorsInterior[i].GetActionWithName("Open_Off").Apply(doorsInterior[i]);
			
			vent.GetActionWithName("Depressurize_On").Apply(vent);
			
			state = 3;
		}
		else if(oxygenLevel >= 0.98)
		{
			for(int i=0; i<doorsInterior.Count; i++)
				doorsInterior[i].GetActionWithName("Open_On").Apply(doorsInterior[i]);
		}
	}
	else if(state == 3) // middle -> out
	{
		if(oxygenLevel > 0.05)
			return;
		else
		{
			for(int i=0; i<doorsExterior.Count; i++)
				doorsExterior[i].GetActionWithName("Open_On").Apply(doorsExterior[i]);
		}
	}
	else if(state == 4) // middle -> in
	{
		if(oxygenLevel < 0.98)
			return;
		else
		{
			for(int i=0; i<doorsInterior.Count; i++)
				doorsInterior[i].GetActionWithName("Open_On").Apply(doorsInterior[i]);
		}
	}
	
	// TODO check timeout
	if(GetTime() > timestampTriggered + TIMEOUT && !sensor.IsActive) // Reset, but don't lock a player in
		Reset();
}

private void Reset()
{
	IMyAirVent vent = GetTypedBlock<IMyAirVent>(BLOCK_VENT);
	List<IMyTerminalBlock> doors = GetTypedBlockGroup<IMyDoor>(GROUP_DOORS);
	
	for(int i=0; i<doors.Count; i++)
		doors[i].GetActionWithName("Open_Off").Apply(doors[i]);
	
	vent.GetActionWithName("Depressurize_On").Apply(vent);

	state = 0;
}

// Emergency lock, locks interior and exterior locks and doesn't allow access until unlocked
private void HandleLock()
{
	if(locked)
		return;
	
	state = 0;
	locked = true;
	
	List<IMyTerminalBlock> doors = GetTypedBlockGroup<IMyDoor>(GROUP_DOORS);
	
	for(int i=0; i<doors.Count; i++)
		doors[i].GetActionWithName("Open_Off").Apply(doors[i]);
	
}

private void HandleUnlock()
{
	locked = false;
}

private void HandleButtonExterior()
{
	if(locked || state > 0)
		return;
	
	timestampTriggered = GetTime();
	state = 1;
	
	IMyAirVent vent = GetTypedBlock<IMyAirVent>(BLOCK_VENT);
	vent.GetActionWithName("Depressurize_On").Apply(vent);
}

private void HandleButtonInterior()
{
	if(locked || state > 0)
		return;
	
	timestampTriggered = GetTime();
	state = 2;
	
	IMyAirVent vent = GetTypedBlock<IMyAirVent>(BLOCK_VENT);
	vent.GetActionWithName("Depressurize_Off").Apply(vent);
}

/* Returns the first type-validated block with the given name
 * Usage: IMyType block = GetTypedBlock<IMyType>(blockname) */
T GetTypedBlock<T>(string targetname) where T : IMyTerminalBlock
{
	List<IMyTerminalBlock> search = new List<IMyTerminalBlock>(); 
	GridTerminalSystem.SearchBlocksOfName(targetname, search);
	
	foreach(IMyTerminalBlock block in search)
		if(block is T)
			return (T)block;
		
	return default(T);
}

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

