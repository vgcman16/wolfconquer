using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Entire native MsgArenaRank10 physical owner/private Handler. No host route added.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgArenaRank10
{
    private static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        try
        {
            LegacyNativeArenaUser[] array = LegacyNativeArenaRankings.Top10.Where((LegacyNativeArenaUser p) => p != null).ToArray();
            int num = array.Length;
            stream.ArenaRank10Create(LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
            for (int num2 = 0; num2 < num; num2++)
            {
                LegacyNativeArenaUser user2 = array[num2];
                stream.AddItemArenaRank10(user2);
            }
            user.Send(stream.ArenaRank10Finalize());
        }
        catch (Exception ex)
        {
            LegacyNativeConsole.WriteLine(ex.ToString());
        }
    }
}
