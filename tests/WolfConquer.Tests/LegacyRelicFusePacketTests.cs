using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRelicFusePacketTests
{
    [Fact]
    public void LegacyRelicFuseOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2356, WorldPacketOpcodes.LegacyRelicFuse);
        Assert.Equal(WorldPacketOpcodes.LegacyRelicFuse, LegacyRelicFusePacket.Opcode);
    }

    [Fact]
    public void LegacyRelicFusePacketWritesRequiredZeroFields()
    {
        var bytes = new LegacyRelicFusePacket(0, 0, 0, 0).ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(LegacyRelicFusePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0x00,
            0x10, 0x00,
            0x18, 0x00,
            0x20, 0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyRelicFusePacketRoundTrips()
    {
        var packet = new LegacyRelicFusePacket(1, 4_294_967_295, 2_222_333, 9_876_543);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyRelicFusePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyRelicFusePacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyAutoHunt, payloadLength: 8);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x01;
        writer.Payload[2] = 0x10;
        writer.Payload[3] = 0x02;
        writer.Payload[4] = 0x18;
        writer.Payload[5] = 0x03;
        writer.Payload[6] = 0x20;
        writer.Payload[7] = 0x04;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRelicFusePacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRelicFusePacketRejectsMissingRequiredRelicID3()
    {
        var writer = new PacketWriter(LegacyRelicFusePacket.Opcode, payloadLength: 6);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x01;
        writer.Payload[2] = 0x10;
        writer.Payload[3] = 0x02;
        writer.Payload[4] = 0x18;
        writer.Payload[5] = 0x03;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRelicFusePacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRelicFusePacketRejectsMalformedProtobuf()
    {
        var writer = new PacketWriter(LegacyRelicFusePacket.Opcode, payloadLength: 1);
        writer.Payload[0] = 0x80;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRelicFusePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
