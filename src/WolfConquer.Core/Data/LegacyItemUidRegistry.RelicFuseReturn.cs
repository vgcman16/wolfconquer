namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // This read issues no UID and does not extend custody beyond the real
    // Runtime/registry lifecycle. The writer rechecks it under its owner.
    internal uint ReadRelicFuseReturnHighWater()
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            return state.HighWater;
        }
    }
}
