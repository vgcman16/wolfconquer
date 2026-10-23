namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact native Player349/367/373/383/385 physical slots. No lazy owner or
    // status-derived replacement; source declarations have implicit UInt32 zero.
    public uint ThundercloudSight;
    public uint CleanSweepPower;
    public uint HookMoonPower;
    public uint AxeShadowPower;
    public uint CelestialDancePower;
}
