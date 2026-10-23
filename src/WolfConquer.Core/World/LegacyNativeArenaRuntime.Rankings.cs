namespace WolfConquer.Core.World;

internal partial class LegacyNativeArenaRuntime
{
    // Whole native INSTANCE MsgArena578..591. Never a static convenience replacement.
    public void CreateRankTop10()
    {
        lock (LegacyNativeArenaRankings.Top10)
        {
            LegacyNativeArenaRankings.Top10 = new LegacyNativeArenaUser[10];
            LegacyNativeArenaUser[] source = LegacyNativeArenaRankings.ArenaPoll.Values.ToArray();
            LegacyNativeArenaUser[] array = LegacyNativeArenaRankingOrder.Descending(source, (LegacyNativeArenaUser p) => p.LastSeasonArenaPoints);
            for (int num = 0; num < array.Length && num != 10; num++)
            {
                LegacyNativeArenaUser user = array[num];
                LegacyNativeArenaRankings.Top10[num] = user;
            }
        }
    }

    // Whole native INSTANCE MsgArena593..605, same physical static rank arrays.
    public void CreateRankTop1000()
    {
        lock (LegacyNativeArenaRankings.Top1000)
        {
            LegacyNativeArenaRankings.Top1000 = new LegacyNativeArenaUser[1000];
            LegacyNativeArenaUser[] source = LegacyNativeArenaRankings.ArenaPoll.Values.ToArray();
            LegacyNativeArenaUser[] array = LegacyNativeArenaRankingOrder.Descending(source, (LegacyNativeArenaUser p) => p.Info.CurrentHonor);
            for (int num = 0; num < array.Length && num != 1000; num++)
            {
                LegacyNativeArenaRankings.Top1000[num] = array[num];
            }
        }
    }
}
