using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgTeamArenaRanking
{
    // Whole native public handler; opcode2186 is not registered with a host here.
    public static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        try
        {
            stream.GetTeamArenaRanking(out var PageNumber);
            LegacyNativeTeamArenaUser[] array = LegacyNativeTeamArenaRankings.Top1000Today.Where((LegacyNativeTeamArenaUser p) => p != null).ToArray();
            int num = (PageNumber - 1) * 10;
            int num2 = Math.Min(10, Math.Max(0, array.Length - num));
            stream.TeamArenaRankingCreate(PageNumber, (ushort)array.Length, LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
            for (int num3 = 0; num3 < num2; num3++)
            {
                if (array.Length > num + num3)
                {
                    LegacyNativeTeamArenaUser user2 = array[num + num3];
                    if (user2.Info.TodayRank < 1000)
                    {
                        stream.AddItemTeamArenaRanking((ushort)(num + num3 + 1), user2.Info.ArenaPoints, user2.Class, user2.Level, user2.GetGender, user2.Name);
                    }
                }
            }
            user.Send(stream.MsgTeamArenaRankingFinalize());
        }
        catch (Exception ex)
        {
            LegacyNativeConsole.WriteLine(ex.ToString());
        }
    }
}
