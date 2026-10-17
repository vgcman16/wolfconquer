using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBlackspotPacketTests
{
    [Fact]
    public void LegacyBlackspotOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2188, WorldPacketOpcodes.LegacyBlackspot);
        Assert.Equal(WorldPacketOpcodes.LegacyBlackspot, LegacyBlackspotPacket.Opcode);
    }

    [Fact]
    public void CreateActiveWritesLegacyFrameShape()
    {
        var packet = LegacyBlackspotPacket.CreateActive(0x01020304);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.True(packet.Active);
        Assert.Equal(0U, packet.DwParam);
        Assert.Equal(LegacyBlackspotPacket.FrameLength, bytes.Length);
        Assert.Equal(LegacyBlackspotPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyBlackspotPacket.PayloadLength, frame.Payload.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(0U, reader.ReadUInt32(0));
        Assert.Equal(0x01020304U, reader.ReadUInt32(4));
        Assert.Equal(
        [
            0x0C, 0x00, 0x8C, 0x08,
            0x00, 0x00, 0x00, 0x00,
            0x04, 0x03, 0x02, 0x01,
        ],
        bytes);
    }

    [Fact]
    public void CreateInactiveWritesLegacyFrameShape()
    {
        var packet = LegacyBlackspotPacket.CreateInactive(0x0A0B0C0D);
        var bytes = packet.ToFrameBytes();

        Assert.False(packet.Active);
        Assert.Equal(1U, packet.DwParam);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(1U, reader.ReadUInt32(0));
        Assert.Equal(0x0A0B0C0DU, reader.ReadUInt32(4));
        Assert.Equal(
        [
            0x0C, 0x00, 0x8C, 0x08,
            0x01, 0x00, 0x00, 0x00,
            0x0D, 0x0C, 0x0B, 0x0A,
        ],
        bytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryParseRoundTrips(bool active)
    {
        var packet = new LegacyBlackspotPacket(Uid: 1_293_600, Active: active);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyBlackspotPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void TryParseRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyBlackspotPacket.Opcode + 1, LegacyBlackspotPacket.PayloadLength);
        writer.WriteUInt32(0, 0);
        writer.WriteUInt32(4, 1_293_600);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyBlackspotPacket.TryParse(frame, out _));
    }

    [Theory]
    [InlineData(LegacyBlackspotPacket.PayloadLength - 1)]
    [InlineData(LegacyBlackspotPacket.PayloadLength + 1)]
    public void TryParseRejectsWrongLength(int payloadLength)
    {
        var writer = new PacketWriter(LegacyBlackspotPacket.Opcode, payloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyBlackspotPacket.TryParse(frame, out _));
    }

    [Fact]
    public void TryParseRejectsUnknownDwParam()
    {
        var writer = new PacketWriter(LegacyBlackspotPacket.Opcode, LegacyBlackspotPacket.PayloadLength);
        writer.WriteUInt32(0, 2);
        writer.WriteUInt32(4, 1_293_600);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyBlackspotPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
