using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLotteryListPacketTests
{
    [Fact]
    public void LegacyLotteryListOpcodeMatchesLegacySourcePacket()
    {
        Assert.Equal((ushort)2432, WorldPacketOpcodes.LegacyLotteryList);
        Assert.Equal(WorldPacketOpcodes.LegacyLotteryList, LegacyLotteryListPacket.Opcode);
    }

    [Fact]
    public void LegacyLotteryListQueryRoundTripsSourceProtobufShape()
    {
        var packet = LegacyLotteryListPacket.Query();
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyLotteryListPacket.Opcode, frame.Header.Opcode);
        Assert.Equal([0x08, 0x00], frame.Payload.ToArray());
        Assert.True(LegacyLotteryListPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.Type);
        Assert.True(parsed.IsAwardListRequest);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyLotteryListEmptyAwardPageUsesSourceProtobufShape()
    {
        var packet = LegacyLotteryListPacket.EmptyAwardPage();
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyLotteryListPacket.Opcode, frame.Header.Opcode);
        Assert.Equal([0x08, 0x00], frame.Payload.ToArray());
        Assert.True(LegacyLotteryListPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.Type);
        Assert.True(parsed.IsAwardListRequest);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyLotteryListNonzeroTypeIsNotAwardListRequest()
    {
        var packet = new LegacyLotteryListPacket(Type: 1, Items: []);

        Assert.False(packet.IsAwardListRequest);
    }

    [Fact]
    public void LegacyLotteryListAwardPageRoundTripsNestedItemShape()
    {
        var packet = new LegacyLotteryListPacket(
            Type: 0,
            Items:
            [
                new LegacyLotteryListItem(
                    Bliss: 3,
                    Id: 17,
                    ItemId: 151,
                    Unknown4: 0,
                    Name: "Sky"),
            ]);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(
        [
            0x08, 0x00,
            0x12, 0x0E,
            0x08, 0x03,
            0x10, 0x11,
            0x18, 0x97, 0x01,
            0x20, 0x00,
            0x2A, 0x03, 0x53, 0x6B, 0x79,
        ], frame.Payload.ToArray());
        Assert.True(LegacyLotteryListPacket.TryParse(frame, out var parsed));
        var item = Assert.Single(parsed.Items);
        Assert.Equal(3U, item.Bliss);
        Assert.Equal(17U, item.Id);
        Assert.Equal(151U, item.ItemId);
        Assert.Equal(0U, item.Unknown4);
        Assert.Equal("Sky", item.Name);
    }

    [Fact]
    public void LegacyLotteryListRejectsMalformedMissingAndUnknownFields()
    {
        Assert.False(LegacyLotteryListPacket.TryParse(
            ReadFrame(new PacketWriter(WorldPacketOpcodes.LegacyLotteryList, payloadLength: 0).ToArray()),
            out _));

        var malformed = new PacketWriter(WorldPacketOpcodes.LegacyLotteryList, payloadLength: 2);
        malformed.Payload[0] = 0x08;
        malformed.Payload[1] = 0x80;
        Assert.False(LegacyLotteryListPacket.TryParse(ReadFrame(malformed.ToArray()), out _));

        var unknown = new PacketWriter(WorldPacketOpcodes.LegacyLotteryList, payloadLength: 4);
        unknown.Payload[0] = 0x08;
        unknown.Payload[1] = 0x00;
        unknown.Payload[2] = 0x78;
        unknown.Payload[3] = 0x01;
        Assert.False(LegacyLotteryListPacket.TryParse(ReadFrame(unknown.ToArray()), out _));

        var missingNestedFields = new PacketWriter(WorldPacketOpcodes.LegacyLotteryList, payloadLength: 6);
        missingNestedFields.Payload[0] = 0x08;
        missingNestedFields.Payload[1] = 0x00;
        missingNestedFields.Payload[2] = 0x12;
        missingNestedFields.Payload[3] = 0x02;
        missingNestedFields.Payload[4] = 0x08;
        missingNestedFields.Payload[5] = 0x01;
        Assert.False(LegacyLotteryListPacket.TryParse(ReadFrame(missingNestedFields.ToArray()), out _));
    }

    [Fact]
    public void LegacyLotteryListRejectsWrongOpcode()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacySourceSilent2433, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x00;

        Assert.False(LegacyLotteryListPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
