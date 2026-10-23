namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact missing native physical slots; no HP/Level/Class/XP or birth replacement.
    // Player355,387/389/393,789/797/833,1033..1041,1059.
    public int SlayerPercent;
    internal LegacyNativeTime32 AbsolutionStamp;
    internal LegacyNativeTime32 NeptunesCurse;
    internal LegacyNativeTime32 SlayerStamp;
    public uint KillerPkPoints = 0u;
    public uint TeamDeathMacthKills = 0u;
    public bool InElitePk = false;
    public ushort Dead_X;
    public ushort Dead_Y;
    public bool GetPkPkPoints = false;
    public DateTime GhostStamp = default(DateTime);
    public DateTime DeadStamp = default(DateTime);
}
