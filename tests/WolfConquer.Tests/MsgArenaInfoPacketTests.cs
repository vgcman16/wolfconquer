using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgArenaInfoPacketTests
{
    [Fact]
    public void MsgArenaInfoStatusDefinesLegacyNumericValues()
    {
        Assert.Equal(0U, (uint)MsgArenaInfoStatus.NotSignedUp);
        Assert.Equal(1U, (uint)MsgArenaInfoStatus.WaitingForOpponent);
        Assert.Equal(2U, (uint)MsgArenaInfoStatus.WaitingInactive);
    }

    [Fact]
    public void MsgArenaInfoOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2191, WorldPacketOpcodes.MsgArenaInfo);
        Assert.Equal(WorldPacketOpcodes.MsgArenaInfo, MsgArenaInfoPacket.Opcode);
    }

    [Fact]
    public void MsgArenaInfoWritesLegacyArenaInfoCreateShape()
    {
        var packet = new MsgArenaInfoPacket(
            TodayRank: 2,
            MsgArenaInfoStatus.WaitingForOpponent,
            TotalWin: 12,
            TotalLose: 3,
            TodayWin: 4,
            TodayBattles: 5,
            HistoryHonor: 600,
            CurrentHonor: 500,
            ArenaPoints: 4500);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(PacketReader.TryRead(frame.Frame.ToArray(), out var reader));
        Assert.Equal(MsgArenaInfoPacket.Opcode, reader.Header.Opcode);
        Assert.Equal(52, reader.Header.Length);
        Assert.Equal(MsgArenaInfoPacket.PayloadLength, reader.Payload.Length);
        Assert.Equal(2U, reader.ReadUInt32(0));
        Assert.Equal(0U, reader.ReadUInt32(4));
        Assert.Equal((uint)MsgArenaInfoStatus.WaitingForOpponent, reader.ReadUInt32(8));
        Assert.Equal(12U, reader.ReadUInt32(12));
        Assert.Equal(3U, reader.ReadUInt32(16));
        Assert.Equal(4U, reader.ReadUInt32(20));
        Assert.Equal(5U, reader.ReadUInt32(24));
        Assert.Equal(600U, reader.ReadUInt32(28));
        Assert.Equal(500U, reader.ReadUInt32(32));
        Assert.Equal(4500U, reader.ReadUInt32(36));
        Assert.Equal(0U, reader.ReadUInt32(40));
        Assert.Equal(0U, reader.ReadUInt32(44));
    }

    [Fact]
    public void MsgArenaInfoRoundTripsLegacyPayload()
    {
        var packet = MsgArenaInfoPacket.FromStatistics(
            new ArenaStatistics(
                TodayRank: 2,
                ArenaPoints: 4500,
                TotalWin: 12,
                TotalLose: 3,
                TodayWin: 4,
                TodayBattles: 5,
                HistoryHonor: 600,
                CurrentHonor: 500),
            MsgArenaInfoStatus.WaitingForOpponent);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgArenaInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
