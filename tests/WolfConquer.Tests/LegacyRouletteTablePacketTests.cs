using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRouletteTablePacketTests
{
    [Fact]
    public void LegacyRouletteTableOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2278, WorldPacketOpcodes.LegacyRouletteTable);
        Assert.Equal(WorldPacketOpcodes.LegacyRouletteTable, LegacyRouletteTablePacket.Opcode);
    }

    [Fact]
    public void LegacyRouletteTablePacketWritesLegacyFixedSlice()
    {
        var packet = new LegacyRouletteTablePacket(
            Uid: 102183,
            TableNumber: 6,
            MoneyType: LegacyRouletteTableMoneyType.Money,
            X: 60,
            Y: 85,
            Mesh: 30236);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyRouletteTablePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyRouletteTablePacket.PayloadLength, frame.Payload.Length);
        Assert.Equal(
        [
            0x27, 0x8F, 0x01, 0x00,
            0x06, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x3C, 0x00,
            0x55, 0x00,
            0x1C, 0x76,
            0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyRouletteTablePacketBuildsFromSeedRouletteRow()
    {
        var table = new RouletteTable(
            Id: 102183,
            Values: [6, 1, 3852, 60, 85, 30236],
            SourceFile: "Roulettes.txt",
            LineNumber: 2,
            RawLine: "102183,6,1,3852,60,85,30236",
            Fields: ["102183", "6", "1", "3852", "60", "85", "30236"]);

        var packet = LegacyRouletteTablePacket.FromRouletteTable(table);

        Assert.Equal(102183U, packet.Uid);
        Assert.Equal(6U, packet.TableNumber);
        Assert.Equal(LegacyRouletteTableMoneyType.Money, packet.MoneyType);
        Assert.Equal(60, packet.X);
        Assert.Equal(85, packet.Y);
        Assert.Equal(30236, packet.Mesh);
        Assert.Equal(0, packet.PlayersCount);
    }

    [Fact]
    public void LegacyRouletteTablePacketRoundTrips()
    {
        var packet = new LegacyRouletteTablePacket(
            Uid: 102191,
            TableNumber: 5,
            MoneyType: LegacyRouletteTableMoneyType.ConquerPoints,
            X: 128,
            Y: 70,
            Mesh: 30236,
            PlayersCount: 3);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyRouletteTablePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyRouletteTablePacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyRouletteTablePacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRouletteTablePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
