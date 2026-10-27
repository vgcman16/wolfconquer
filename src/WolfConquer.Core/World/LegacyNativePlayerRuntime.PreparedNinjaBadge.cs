namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // The Ninja operation validates the physical owner/current byte and prepares
    // its exact notification before entering this plain-store commit phase.
    internal void ApplyPreparedNinjaBadge(byte value) => _NiniaP0 = value;
}
