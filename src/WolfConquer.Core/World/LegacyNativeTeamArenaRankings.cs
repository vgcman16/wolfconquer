using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// All native MsgTeamArena static state628..634 and full static UpdateRank659..677.
// No instance tournament constructor/Match/Team facade is declared.
internal static class LegacyNativeTeamArenaRankings
{
    public static ConcurrentDictionary<uint, LegacyNativeTeamArenaUser> ArenaPoll = new();
    public static LegacyNativeTeamArenaUser[] Top10 = new LegacyNativeTeamArenaUser[10];
    public static LegacyNativeTeamArenaUser[] Top1000Today = new LegacyNativeTeamArenaUser[1000];
    public static LegacyNativeTeamArenaUser[] Top1000 = new LegacyNativeTeamArenaUser[1000];

    public static void UpdateRank()
    {
        lock (Top1000Today)
        {
            Top1000Today = new LegacyNativeTeamArenaUser[1000];
            LegacyNativeTeamArenaUser[] array = ArenaPoll.Values.ToArray();
            LegacyNativeTeamArenaUser[] array2 = array;
            foreach (LegacyNativeTeamArenaUser user in array2)
            {
                user.Info.TodayRank = 0u;
            }
            LegacyNativeTeamArenaUser[] array3 = array.OrderByDescending((LegacyNativeTeamArenaUser p) => p.Info.ArenaPoints).ToArray();
            for (int num = 0; num < array3.Length && num != 1000; num++)
            {
                Top1000Today[num] = array3[num];
                array3[num].Info.TodayRank = (uint)(num + 1);
            }
        }
    }
}
