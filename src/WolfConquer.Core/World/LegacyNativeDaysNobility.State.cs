namespace WolfConquer.Core.World;

// Complete native DaysNobility owner. PayNobilitySystem birth and native
// SaveClient/LoadCharacter/expiry/startup callers are deliberately unwired.
internal partial class LegacyNativeDaysNobility
{
    private LegacyNativePlayerRuntime Owner;

    private bool Active = false;

    private uint id;

    private ulong lastdonation;

    private LegacyNativeNobilityRank paidrnk;

    private long periodtick;

    public LegacyNativeDaysNobility(LegacyNativePlayerRuntime client)
    {
        Owner = client;
        id = client.UID;
    }
}
