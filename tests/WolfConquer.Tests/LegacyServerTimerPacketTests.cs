using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyServerTimerPacketTests
{
    [Fact]
    public void LegacyServerTimerOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2224, WorldPacketOpcodes.LegacyServerTimer);
        Assert.Equal(WorldPacketOpcodes.LegacyServerTimer, LegacyServerTimerPacket.Opcode);
    }

    [Fact]
    public void LegacyServerTimerFromDateTimeMatchesLegacyDateMath()
    {
        var packet = LegacyServerTimerPacket.FromDateTime(new DateTime(2026, 6, 4, 13, 27, 59));

        Assert.Equal(126, packet.Year);
        Assert.Equal(5, packet.Month);
        Assert.Equal(155, packet.DayOfYear);
        Assert.Equal(4, packet.DayOfMonth);
        Assert.Equal(13, packet.Hour);
        Assert.Equal(27, packet.Minute);
        Assert.Equal(59, packet.Second);
    }

    [Fact]
    public void LegacyServerTimerPacketWritesLegacyProtobufNetShape()
    {
        var packet = LegacyServerTimerPacket.FromDateTime(new DateTime(2026, 6, 4, 13, 27, 59));
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(21, bytes.Length);
        Assert.Equal(LegacyServerTimerPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(21, frame.Header.Length);
        Assert.Equal(17, frame.Payload.Length);
        Assert.Equal(
        [
            0x15, 0x00, 0xB0, 0x08,
            0x08, 0x00,
            0x10, 0x7E,
            0x18, 0x05,
            0x20, 0x9B, 0x01,
            0x28, 0x04,
            0x30, 0x0D,
            0x38, 0x1B,
            0x40, 0x3B
        ],
        bytes);
    }

    [Fact]
    public void LegacyServerTimerPacketRoundTrips()
    {
        var packet = LegacyServerTimerPacket.FromDateTime(new DateTime(2026, 6, 4, 13, 27, 59));
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyServerTimerPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyServerTimerPacketRejectsMissingRequiredFields()
    {
        var writer = new PacketWriter(LegacyServerTimerPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x10;
        writer.Payload[1] = 0x7E;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyServerTimerPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
