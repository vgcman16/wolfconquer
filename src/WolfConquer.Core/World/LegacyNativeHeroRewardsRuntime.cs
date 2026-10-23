using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// The real goal owner; reward/prize execution and host adoption are separate.
internal partial class LegacyNativeHeroRewardsRuntime
{
    public class Item
    {
        public uint UID;
        public uint UnKnow;
        public byte Claim;
    }

    private readonly LegacyNativeGameClientRuntime Owner;
    private readonly object goalSyncRoot = new();
    private const int MaxGoalRecords = 65535;
    private const int MaxGoalRecordLength = 1024 * 1024;

    public LegacyNativeDbSafeDictionary<uint, Item> ArrayGoals = new();

    public LegacyNativeHeroRewardsRuntime(LegacyNativeGameClientRuntime owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
        // Native client birth creates this owner BEFORE Player. Do not infer a
        // permanent Player/UID authority from the constructor's early position.
    }
}
