using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgTeamArenaInfo: physical state, Send/Create and handler.
internal class LegacyNativeMsgTeamArenaInfo
{
    public enum Action : uint { NotSignedUp, WaitingForOpponent, WaitingInactive }
    public uint TodayRank;
    public Action Status;
    public uint TotalWin;
    public uint TotalLose;
    public uint TodayWin;
    public uint TodayBattles;
    public uint HistoryHonor;
    public uint CurrentHonor;
    public uint ArenaPoints;
    public uint UnKnow2;
    public uint UnKnow3;

    public void Send(LegacyNativeGameClientRuntime client)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        client.Send(stream.TeamArenaInfoCreate(this));
    }

    public static LegacyNativeMsgTeamArenaInfo Create()
    {
        return new LegacyNativeMsgTeamArenaInfo();
    }

    // Whole original receiver, not host packet registration.
    private static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        user.TeamArenaStatistic!.Info.Send(user);
    }
}
