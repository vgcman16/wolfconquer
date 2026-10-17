using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyServerConfigPacketTests
{
    [Fact]
    public void LegacyServerConfigOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2047, WorldPacketOpcodes.LegacyServerConfig);
        Assert.Equal(WorldPacketOpcodes.LegacyServerConfig, LegacyServerConfigPacket.Opcode);
    }

    [Fact]
    public void LegacyServerConfigPacketWritesFixedLegacyShape()
    {
        var packet = new LegacyServerConfigPacket();
        var bytes = packet.ToFrameBytes();

        Assert.Equal(LegacyServerConfigPacket.FrameLength, bytes.Length);
        Assert.Equal(PacketHeader.Size + LegacyServerConfigPacket.PayloadLength, bytes.Length);

        var frame = ReadFrame(bytes);
        Assert.True(PacketReader.TryRead(bytes, out var reader));

        Assert.Equal(LegacyServerConfigPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyServerConfigPacket.FrameLength, frame.Header.Length);
        Assert.Equal(LegacyServerConfigPacket.PayloadLength, frame.Payload.Length);
        Assert.Equal((uint)0, reader.ReadUInt32(0));
        Assert.Equal((uint)0, reader.ReadUInt32(4));
        Assert.Equal((uint)15, reader.ReadUInt32(8));
        Assert.Equal((uint)9999, reader.ReadUInt32(12));
        Assert.Equal((uint)3, reader.ReadUInt32(16));
        Assert.Equal((byte)1, reader.Payload[20]);
        Assert.True(LegacyServerConfigPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyServerConfigPacketWritesExpectedLittleEndianBytes()
    {
        var packet = new LegacyServerConfigPacket();

        Assert.Equal(
        [
            0x19, 0x00, 0xFF, 0x07,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x0F, 0x00, 0x00, 0x00,
            0x0F, 0x27, 0x00, 0x00,
            0x03, 0x00, 0x00, 0x00,
            0x01
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
