using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyServerVersionPacketTests
{
    [Fact]
    public void LegacyServerVersionOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2161, WorldPacketOpcodes.LegacyServerVersion);
        Assert.Equal(WorldPacketOpcodes.LegacyServerVersion, LegacyServerVersionPacket.Opcode);
    }

    [Fact]
    public void LegacyServerVersionPacketWritesFixedLegacyShape()
    {
        var packet = new LegacyServerVersionPacket();
        var bytes = packet.ToFrameBytes();

        Assert.Equal(LegacyServerVersionPacket.FrameLength, bytes.Length);
        Assert.Equal(PacketHeader.Size + LegacyServerVersionPacket.PayloadLength, bytes.Length);

        var frame = ReadFrame(bytes);
        Assert.True(PacketReader.TryRead(bytes, out var reader));

        Assert.Equal(LegacyServerVersionPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyServerVersionPacket.FrameLength, frame.Header.Length);
        Assert.Equal(LegacyServerVersionPacket.PayloadLength, frame.Payload.Length);
        Assert.Equal((uint)0, reader.ReadUInt32(0));
        Assert.Equal((uint)0, reader.ReadUInt32(4));
        Assert.True(LegacyServerVersionPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyServerVersionPacketWritesExpectedLittleEndianBytes()
    {
        var packet = new LegacyServerVersionPacket();

        Assert.Equal(
        [
            0x0C, 0x00, 0x71, 0x08,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ],
        packet.ToFrameBytes());
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
