using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// All native MsgArena static state554..560 plus entire static UpdateRank770..789.
// This owner is not an instance MsgArena constructor or tournament scheduler.
internal static class LegacyNativeArenaRankings
{
    public static ConcurrentDictionary<uint, LegacyNativeArenaUser> ArenaPoll = new();
    public static LegacyNativeArenaUser[] Top10 = new LegacyNativeArenaUser[10];
    public static LegacyNativeArenaUser[] Top1000Today = new LegacyNativeArenaUser[1000];
    public static LegacyNativeArenaUser[] Top1000 = new LegacyNativeArenaUser[1000];

    public static void UpdateRank()
    {
        lock (Top1000Today)
        {
            Top1000Today = new LegacyNativeArenaUser[1000];
            LegacyNativeArenaUser[] array = ArenaPoll.Values.ToArray();
            LegacyNativeArenaUser[] array2 = array;
            foreach (LegacyNativeArenaUser user in array2)
            {
                user.Info.TodayRank = 0u;
            }
            LegacyNativeArenaUser[] array3 = array.OrderByDescending((LegacyNativeArenaUser p) => p.Info.ArenaPoints).ToArray();
            for (int num = 0; num < array3.Length && num != 1000; num++)
            {
                Top1000Today[num] = array3[num];
                array3[num].Info.TodayRank = (uint)(num + 1);
            }
        }
    }
}
