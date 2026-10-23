using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgTeamArenaRank10
{
    // Whole native private handler; opcode2287 is not registered with a host here.
    private static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        try
        {
            LegacyNativeTeamArenaUser[] array = LegacyNativeTeamArenaRankings.Top10.Where((LegacyNativeTeamArenaUser p) => p != null).ToArray();
            int num = array.Length;
            stream.TeamArenaRank10Create(LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
            for (int num2 = 0; num2 < num; num2++)
            {
                LegacyNativeTeamArenaUser user2 = array[num2];
                stream.AddItemTeamArenaRank10(user2);
            }
            stream.TeamArenaRank10Finalize();
            user.Send(stream);
        }
        catch (Exception ex)
        {
            LegacyNativeConsole.WriteLine(ex.ToString());
        }
    }
}
