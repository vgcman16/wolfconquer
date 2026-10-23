namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient145 physical reference, no constructor assignment in source.
    // Whole OnDequeue owns future publication; this is not ComputeWeapons projection.
    internal Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>? Weapons;
}
