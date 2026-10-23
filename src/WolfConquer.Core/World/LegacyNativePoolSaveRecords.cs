using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Actual native Pool70/72 slots in source order. No loader/startup invocation.
internal static class LegacyNativePoolSaveRecords
{
    public static LegacyNativeActivityTaskTable ActivityTasks = new LegacyNativeActivityTaskTable();
    public static LegacyNativeInfoHeroRewardTable TableHeroRewards = new LegacyNativeInfoHeroRewardTable();
}
