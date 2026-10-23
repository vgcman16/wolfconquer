namespace WolfConquer.Core.World;

// The private native names remain intact for the genuine caller family.
internal partial class LegacyNativePerfectionEffects
{
    private static void MirrorOfSin(LegacyNativePlayerRuntime target) => TryMirrorOfSin(target);

    private static void DrainingTouch(LegacyNativeGameClientRuntime? client)
    {
    }

    private static void KillingFlash(LegacyNativeGameClientRuntime client) => TryKillingFlash(client);
}
