using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPokerTablePacketTests
{
    [Fact]
    public void LegacyPokerTableOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2015, WorldPacketOpcodes.LegacyPokerTable);
        Assert.Equal(WorldPacketOpcodes.LegacyPokerTable, LegacyPokerTablePacket.Opcode);
    }

    [Fact]
    public void LegacyPokerTablePacketWritesLegacyShowHandTableFixedSlice()
    {
        var packet = new LegacyPokerTablePacket(
            Uid: 102130,
            X: 194,
            Y: 128,
            Mesh: 7247567,
            TableNumber: 13,
            Unlimited: true,
            IsCps: false,
            MinBet: 10_000,
            State: 0,
            TotalPot: 0,
            Players: []);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyPokerTablePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyPokerTablePacket.BasePayloadLength, frame.Payload.Length);
        Assert.Equal(
        [
            0xF2, 0x8E, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0xC2, 0x00,
            0x80, 0x00,
            0xCF, 0x96, 0x6E, 0x00,
            0x00, 0x00,
            0x0D, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x10, 0x27, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyPokerTablePacketBuildsFromSeedPokerTableRow()
    {
        var table = new PokerTable(
            Id: 102130,
            Values: [194, 128, 7247567, 13, 1, 0, 10000, 1],
            SourceFile: "PokerTables.txt",
            LineNumber: 1,
            RawLine: "102130 194 128 7247567 013 1 0 10000 1",
            Fields: ["102130", "194", "128", "7247567", "013", "1", "0", "10000", "1"]);

        var packet = LegacyPokerTablePacket.FromPokerTable(table);

        Assert.Equal(102130U, packet.Uid);
        Assert.Equal(194, packet.X);
        Assert.Equal(128, packet.Y);
        Assert.Equal(7247567U, packet.Mesh);
        Assert.Equal(13U, packet.TableNumber);
        Assert.True(packet.Unlimited);
        Assert.False(packet.IsCps);
        Assert.Equal(10_000UL, packet.MinBet);
        Assert.Equal(LegacyPokerTablePacket.ShowHandMapId, packet.MapId);
    }

    [Fact]
    public void LegacyPokerTablePacketUsesCpsMapForCpsRows()
    {
        var table = new PokerTable(
            Id: 102230,
            Values: [326, 0, 1002967, 99, 1, 1, 50000000, 1],
            SourceFile: "PokerTables.txt",
            LineNumber: 50,
            RawLine: "102230 326 0 1002967 099 1 1 50000000 1",
            Fields: ["102230", "326", "0", "1002967", "099", "1", "1", "50000000", "1"]);

        var packet = LegacyPokerTablePacket.FromPokerTable(table);

        Assert.True(packet.IsCps);
        Assert.Equal(LegacyPokerTablePacket.CpsMapId, packet.MapId);
    }

    [Fact]
    public void LegacyPokerTablePacketRoundTripsPlayers()
    {
        var packet = new LegacyPokerTablePacket(
            Uid: 102130,
            X: 194,
            Y: 128,
            Mesh: 7247567,
            TableNumber: 13,
            Unlimited: true,
            IsCps: false,
            MinBet: 10_000,
            State: 2,
            TotalPot: 123_456,
            Players:
            [
                new LegacyPokerTablePlayer(1293600, Seat: 4, Status: 1),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyPokerTablePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.Uid, parsed.Uid);
        Assert.Equal(packet.X, parsed.X);
        Assert.Equal(packet.Y, parsed.Y);
        Assert.Equal(packet.Mesh, parsed.Mesh);
        Assert.Equal(packet.TableNumber, parsed.TableNumber);
        Assert.Equal(packet.Unlimited, parsed.Unlimited);
        Assert.Equal(packet.IsCps, parsed.IsCps);
        Assert.Equal(packet.MinBet, parsed.MinBet);
        Assert.Equal(packet.State, parsed.State);
        Assert.Equal(packet.TotalPot, parsed.TotalPot);
        Assert.Equal(packet.Players, parsed.Players);
    }

    [Fact]
    public void LegacyPokerTablePacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyPokerTablePacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyPokerTablePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
