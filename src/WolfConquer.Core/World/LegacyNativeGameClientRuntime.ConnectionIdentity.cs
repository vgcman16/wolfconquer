namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Exact native GameClient219, reused by the full Teleport successor.
    public bool ProjectManager = false;

    // Exact native GameClient239; no authentication, UID binding or constructor shortcut.
    public uint ConnectionUID = 0u;
}
