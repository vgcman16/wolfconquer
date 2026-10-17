using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyServerInfoPacketTests
{
    [Fact]
    public void LegacyServerInfoPacketUsesLegacyServerTimerOpcode()
    {
        Assert.Equal((ushort)2224, LegacyServerInfoPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyServerTimer, LegacyServerInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyServerInfoPacketWritesVigorAsTypeTwoYearField()
    {
        var packet = new LegacyServerInfoPacket(998);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(21, bytes.Length);
        Assert.Equal(LegacyServerInfoPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(21, frame.Header.Length);
        Assert.Equal(17, frame.Payload.Length);
        Assert.Equal(
        [
            0x15, 0x00, 0xB0, 0x08,
            0x08, 0x02,
            0x10, 0xE6, 0x07,
            0x18, 0x00,
            0x20, 0x00,
            0x28, 0x00,
            0x30, 0x00,
            0x38, 0x00,
            0x40, 0x00,
        ],
        bytes);
    }

    [Fact]
    public void LegacyServerInfoPacketParsesSyntheticVigorZeroFrame()
    {
        // Construct all eight protobuf fields with zero values, then set the server-info type.
        var expected = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 2224);
        for (var field = 1; field <= 8; field++)
        {
            expected[4 + (field - 1) * 2] = (byte)(field << 3);
        }
        expected[5] = 2;
        var frame = ReadFrame(expected);

        Assert.True(LegacyServerInfoPacket.TryParse(frame, out var packet));

        Assert.Equal(0U, packet.Vigor);
        Assert.Equal(expected, packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyServerInfoPacketRoundTrips()
    {
        var packet = new LegacyServerInfoPacket(1);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyServerInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyServerInfoPacketRejectsTimerType()
    {
        var timer = LegacyServerTimerPacket.FromDateTime(new DateTime(2026, 6, 4, 13, 27, 59));
        var frame = ReadFrame(timer.ToFrameBytes());

        Assert.False(LegacyServerInfoPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyServerInfoPacketRejectsMissingVigor()
    {
        var writer = new PacketWriter(LegacyServerInfoPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x02;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyServerInfoPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
