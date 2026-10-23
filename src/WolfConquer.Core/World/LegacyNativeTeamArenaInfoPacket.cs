using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeTeamArenaInfoPacket
{
    // Whole native MsgBuilder2883..2899.
    public static LegacyNativePacket TeamArenaInfoCreate(this LegacyNativePacket stream, LegacyNativeMsgTeamArenaInfo info)
    {
        stream.InitWriter();
        stream.Write(info.TodayRank);
        stream.Write((uint)info.Status);
        stream.Write(info.TotalWin);
        stream.Write(info.TotalLose);
        stream.Write(info.TodayWin);
        stream.Write(info.TodayBattles);
        stream.Write(info.HistoryHonor);
        stream.Write(info.CurrentHonor);
        stream.Write(info.ArenaPoints);
        stream.Write(info.UnKnow2);
        stream.Write(info.UnKnow3);
        stream.Finalize(2238);
        return stream;
    }
}
