namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient271 physical field. Birth is the conditional LoadCharacter1106 phase,
    // not the GameClient constructor. Current partial client birth leaves this null.
    internal LegacyNativeHundredWeapons? HundredWeapons;
}
