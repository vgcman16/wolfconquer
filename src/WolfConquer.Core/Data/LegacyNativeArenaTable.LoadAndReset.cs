using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Whole native ArenaTable18..62. The actual default-null scheduler reference
// remains unchecked; source Create is not invoked and no fallback Arena is born.
internal partial class LegacyNativeArenaTable
{
    internal void Load()
    {
        using (LegacyNativeDbRead read = new LegacyNativeDbRead("Arena.ini"))
        {
            if (read.Reader())
            {
                for (int i = 0; i < read.Count; i++)
                {
                    LegacyNativeArenaUser user = new LegacyNativeArenaUser();
                    user.Load(read.ReadString(""));
                    LegacyNativeArenaRankings.ArenaPoll.TryAdd(user.UID, user);
                }
            }
        }
        LegacyNativeMsgSchedules.Arena!.CreateRankTop10();
        LegacyNativeMsgSchedules.Arena!.CreateRankTop1000();
        LegacyNativeArenaRankings.UpdateRank();
    }

    internal void ResetArena()
    {
        foreach (LegacyNativeArenaUser value in LegacyNativeArenaRankings.ArenaPoll.Values)
        {
            value.LastSeasonArenaPoints = value.Info.ArenaPoints;
            value.LastSeasonWin = value.Info.TodayWin;
            value.LastSeasonLose = value.Info.TotalLose;
            value.LastSeasonRank = value.Info.TodayRank;
            if (value.Info.TodayRank != 0 && value.Info.ArenaPoints != 4000)
            {
                value.Info.CurrentHonor += value.Info.ArenaPoints * 5;
            }
            if (value.Info.HistoryHonor < value.Info.CurrentHonor)
            {
                value.Info.HistoryHonor = value.Info.CurrentHonor;
            }
            value.Info.TodayWin = 0u;
            value.Info.TodayBattles = 0u;
            value.Info.TotalLose = 0u;
            value.Info.TodayRank = 0u;
            value.Info.ArenaPoints = 4000u;
        }
        LegacyNativeMsgSchedules.Arena!.CreateRankTop10();
        LegacyNativeMsgSchedules.Arena!.CreateRankTop1000();
        LegacyNativeArenaRankings.UpdateRank();
    }
}
