using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgArenaInfoStatus : uint
{
    NotSignedUp = 0,
    WaitingForOpponent = 1,
    WaitingInactive = 2
}

public readonly record struct MsgArenaInfoPacket(
    uint TodayRank,
    MsgArenaInfoStatus Status,
    uint TotalWin,
    uint TotalLose,
    uint TodayWin,
    uint TodayBattles,
    uint HistoryHonor,
    uint CurrentHonor,
    uint ArenaPoints)
{
    private const int TodayRankOffset = 0;
    private const int Reserved0Offset = TodayRankOffset + sizeof(uint);
    private const int StatusOffset = Reserved0Offset + sizeof(uint);
    private const int TotalWinOffset = StatusOffset + sizeof(uint);
    private const int TotalLoseOffset = TotalWinOffset + sizeof(uint);
    private const int TodayWinOffset = TotalLoseOffset + sizeof(uint);
    private const int TodayBattlesOffset = TodayWinOffset + sizeof(uint);
    private const int HistoryHonorOffset = TodayBattlesOffset + sizeof(uint);
    private const int CurrentHonorOffset = HistoryHonorOffset + sizeof(uint);
    private const int ArenaPointsOffset = CurrentHonorOffset + sizeof(uint);
    private const int Reserved1Offset = ArenaPointsOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.MsgArenaInfo;
    public const int PayloadLength = Reserved1Offset + (sizeof(uint) * 2);

    public static MsgArenaInfoPacket FromStatistics(ArenaStatistics statistics, MsgArenaInfoStatus status)
    {
        return new MsgArenaInfoPacket(
            statistics.TodayRank,
            status,
            statistics.TotalWin,
            statistics.TotalLose,
            statistics.TodayWin,
            statistics.TodayBattles,
            statistics.HistoryHonor,
            statistics.CurrentHonor,
            statistics.ArenaPoints);
    }

    public static bool TryParse(PacketFrame frame, out MsgArenaInfoPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new MsgArenaInfoPacket(
            reader.ReadUInt32(TodayRankOffset),
            (MsgArenaInfoStatus)reader.ReadUInt32(StatusOffset),
            reader.ReadUInt32(TotalWinOffset),
            reader.ReadUInt32(TotalLoseOffset),
            reader.ReadUInt32(TodayWinOffset),
            reader.ReadUInt32(TodayBattlesOffset),
            reader.ReadUInt32(HistoryHonorOffset),
            reader.ReadUInt32(CurrentHonorOffset),
            reader.ReadUInt32(ArenaPointsOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(TodayRankOffset, TodayRank);
        writer.WriteUInt32(Reserved0Offset, 0);
        writer.WriteUInt32(StatusOffset, (uint)Status);
        writer.WriteUInt32(TotalWinOffset, TotalWin);
        writer.WriteUInt32(TotalLoseOffset, TotalLose);
        writer.WriteUInt32(TodayWinOffset, TodayWin);
        writer.WriteUInt32(TodayBattlesOffset, TodayBattles);
        writer.WriteUInt32(HistoryHonorOffset, HistoryHonor);
        writer.WriteUInt32(CurrentHonorOffset, CurrentHonor);
        writer.WriteUInt32(ArenaPointsOffset, ArenaPoints);
        writer.WriteUInt32(Reserved1Offset, 0);
        writer.WriteUInt32(Reserved1Offset + sizeof(uint), 0);
        return writer.ToArray();
    }
}
