using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeArenaInfoPacket
{
    // Whole native MsgBuilder679..695. Length/PacketID fields are not projected.
    public static LegacyNativePacket ArenaInfoCreate(this LegacyNativePacket stream, LegacyNativeMsgArenaInfo user)
    {
        stream.InitWriter();
        stream.Write(user.TodayRank);
        stream.Write(0u);
        stream.Write((uint)user.Status);
        stream.Write(user.TotalWin);
        stream.Write(user.TotalLose);
        stream.Write(user.TodayWin);
        stream.Write(user.TodayBattles);
        stream.Write(user.HistoryHonor);
        stream.Write(user.CurrentHonor);
        stream.Write(user.ArenaPoints);
        stream.ZeroFill(8);
        stream.Finalize(2191);
        return stream;
    }
}
