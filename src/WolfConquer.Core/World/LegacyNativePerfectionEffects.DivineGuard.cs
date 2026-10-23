namespace WolfConquer.Core.World;

// Whole selected-trigger operation; outer CheckEffects eligibility remains held.
internal partial class LegacyNativePerfectionEffects
{
    private static void DivineGuard(LegacyNativeGameClientRuntime client) => TryDivineGuard(client);
}
