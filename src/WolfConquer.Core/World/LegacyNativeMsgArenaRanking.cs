using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native physical owner/private receiver. No bounds repair or host registration.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgArenaRanking
{
    public enum RankTyp : ushort { Today, History }

    private static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        stream.GetArenaRanking(out var rank, out var PageNumber);
        switch (rank)
        {
            case RankTyp.Today:
            {
                LegacyNativeArenaUser[] array2 = LegacyNativeArenaRankings.Top1000Today.Where((LegacyNativeArenaUser p) => p != null).ToArray();
                int num4 = (PageNumber - 1) * 8;
                int num5 = Math.Min(8, Math.Max(0, array2.Length - num4));
                stream.ArenaRankingCreate(RankTyp.Today, PageNumber, (uint)array2.Length, LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
                for (int num6 = 0; num6 < num5; num6++)
                {
                    if (array2.Length > num4 + num6)
                    {
                        LegacyNativeArenaUser user3 = array2[num4 + num6];
                        if (user3.Info.TodayRank < 1000)
                        {
                            stream.AddItemArenaRanking((ushort)(num4 + num6 + 1), user3.Name, 0,
                                user3.Info.ArenaPoints, user3.Class, user3.Level, 1u, user3.Mesh);
                        }
                    }
                }
                user.Send(stream.ArenaRankingFinalize());
                break;
            }
            case RankTyp.History:
            {
                LegacyNativeArenaUser[] array = LegacyNativeArenaRankings.Top1000.Where((LegacyNativeArenaUser p) => p != null).ToArray();
                int num = (PageNumber - 1) * 8;
                int num2 = Math.Min(8, Math.Max(0, array.Length - num));
                stream.ArenaRankingCreate(RankTyp.History, PageNumber, (uint)array.Length, LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
                for (int num3 = 0; num3 < num2; num3++)
                {
                    if (array.Length > num + num3)
                    {
                        LegacyNativeArenaUser user2 = array[num + num3];
                        if (user2.Info.TodayRank < 1000)
                        {
                            stream.AddItemArenaRanking((ushort)(num + num3 + 1), user2.Name, 6004,
                                user2.Info.HistoryHonor, user2.Class, user2.Level, 33942209u, user2.Mesh);
                        }
                    }
                }
                user.Send(stream.ArenaRankingFinalize());
                break;
            }
        }
    }
}
