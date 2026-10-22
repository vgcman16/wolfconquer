namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient41. The actual MyCombatGear constructor-slot phase remains unwired.
#pragma warning disable CS0649
    internal LegacyNativeMyCombatGear? CombatGear;
#pragma warning restore CS0649
}
