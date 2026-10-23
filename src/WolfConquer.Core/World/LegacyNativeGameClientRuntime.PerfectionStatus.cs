using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient137,143,147: state only; the complete rebuild is held, not narrowed.
    internal LegacyNativePrestigeRanking.Entry? PrestrigeEntry;

    // Native GameClient139: eager physical owner, not the modern query snapshot cache.
    internal LegacyNativePerfectionEffect PerfectionStatus = new LegacyNativePerfectionEffect();

    public uint[] PrestigePoints = new uint[43];

    public uint MyPrestigePoints = 0u;
}
