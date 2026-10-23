namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Exact native GameClient181 implicit-null physical slot. Real Match ctor owns writes.
    internal LegacyNativeArenaMatchRuntime? ArenaMatch;
}
