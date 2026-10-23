using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgArenaInfo physical owner/implicit constructor/handler.
internal class LegacyNativeMsgArenaInfo
{
    public enum Action : uint { NotSignedUp, WaitingForOpponent, WaitingInactive }
    public ushort Length;
    public ushort PacketID;
    public uint TodayRank;
    public Action Status;
    public uint TotalWin;
    public uint TotalLose;
    public uint TodayWin;
    public uint TodayBattles;
    public uint HistoryHonor;
    public uint CurrentHonor;
    public uint ArenaPoints;

    // Native private handler; no Packet attribute/host route is installed.
    private static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        user.Send(stream.ArenaInfoCreate(user.ArenaStatistic!.Info));
    }
}
