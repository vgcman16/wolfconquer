using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTeamArenaInfoStatus : uint
{
    NotSignedUp = 0,
    WaitingForOpponent = 1,
    WaitingInactive = 2,
}

public readonly record struct LegacyTeamArenaInfoPacket(
    uint TodayRank,
    LegacyTeamArenaInfoStatus Status,
    uint TotalWin,
    uint TotalLose,
    uint TodayWin,
    uint TodayBattles,
    uint HistoryHonor,
    uint CurrentHonor,
    uint ArenaPoints,
    uint Unknown2,
    uint Unknown3)
{
    private const int TodayRankOffset = 0;
    private const int StatusOffset = TodayRankOffset + sizeof(uint);
    private const int TotalWinOffset = StatusOffset + sizeof(uint);
    private const int TotalLoseOffset = TotalWinOffset + sizeof(uint);
    private const int TodayWinOffset = TotalLoseOffset + sizeof(uint);
    private const int TodayBattlesOffset = TodayWinOffset + sizeof(uint);
    private const int HistoryHonorOffset = TodayBattlesOffset + sizeof(uint);
    private const int CurrentHonorOffset = HistoryHonorOffset + sizeof(uint);
    private const int ArenaPointsOffset = CurrentHonorOffset + sizeof(uint);
    private const int Unknown2Offset = ArenaPointsOffset + sizeof(uint);
    private const int Unknown3Offset = Unknown2Offset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamArenaInfo;
    public const int PayloadLength = Unknown3Offset + sizeof(uint);

    public static LegacyTeamArenaInfoPacket FromStatistics(
        TeamArenaStatistics statistics,
        LegacyTeamArenaInfoStatus status)
    {
        return new LegacyTeamArenaInfoPacket(
            statistics.TodayRank,
            status,
            statistics.TotalWin,
            statistics.TotalLose,
            statistics.TodayWin,
            statistics.TodayBattles,
            statistics.HistoryHonor,
            statistics.CurrentHonor,
            statistics.ArenaPoints,
            statistics.Unknown2,
            statistics.Unknown3);
    }

    public static bool IsQuery(PacketFrame frame)
    {
        return frame.Header.Opcode == Opcode;
    }

    public static byte[] QueryFrameBytes()
    {
        return new PacketWriter(Opcode, payloadLength: 0).ToArray();
    }

    public static bool TryParse(PacketFrame frame, out LegacyTeamArenaInfoPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyTeamArenaInfoPacket(
            reader.ReadUInt32(TodayRankOffset),
            (LegacyTeamArenaInfoStatus)reader.ReadUInt32(StatusOffset),
            reader.ReadUInt32(TotalWinOffset),
            reader.ReadUInt32(TotalLoseOffset),
            reader.ReadUInt32(TodayWinOffset),
            reader.ReadUInt32(TodayBattlesOffset),
            reader.ReadUInt32(HistoryHonorOffset),
            reader.ReadUInt32(CurrentHonorOffset),
            reader.ReadUInt32(ArenaPointsOffset),
            reader.ReadUInt32(Unknown2Offset),
            reader.ReadUInt32(Unknown3Offset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(TodayRankOffset, TodayRank);
        writer.WriteUInt32(StatusOffset, (uint)Status);
        writer.WriteUInt32(TotalWinOffset, TotalWin);
        writer.WriteUInt32(TotalLoseOffset, TotalLose);
        writer.WriteUInt32(TodayWinOffset, TodayWin);
        writer.WriteUInt32(TodayBattlesOffset, TodayBattles);
        writer.WriteUInt32(HistoryHonorOffset, HistoryHonor);
        writer.WriteUInt32(CurrentHonorOffset, CurrentHonor);
        writer.WriteUInt32(ArenaPointsOffset, ArenaPoints);
        writer.WriteUInt32(Unknown2Offset, Unknown2);
        writer.WriteUInt32(Unknown3Offset, Unknown3);
        return writer.ToArray();
    }
}
