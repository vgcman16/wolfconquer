using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMsgLoginGamePacketTests
{
    [Fact]
    public void LegacyMsgLoginGameOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2065, WorldPacketOpcodes.LegacyMsgLoginGame);
        Assert.Equal(WorldPacketOpcodes.LegacyMsgLoginGame, LegacyMsgLoginGamePacket.Opcode);
    }

    [Fact]
    public void LegacyMsgLoginGamePacketWritesExpectedLittleEndianBytes()
    {
        var packet = new LegacyMsgLoginGamePacket(0xA1B2C3D4);

        Assert.Equal(
        [
            0x08, 0x00, 0x11, 0x08,
            0xD4, 0xC3, 0xB2, 0xA1
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyMsgLoginGamePacketRoundTrips()
    {
        var packet = new LegacyMsgLoginGamePacket(0x12345678);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyMsgLoginGamePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyMsgLoginGamePacketParsesSyntheticExtendedPayload()
    {
        var writer = new PacketWriter(LegacyMsgLoginGamePacket.Opcode, 48);
        writer.WriteUInt32(0, 0x12345678);
        for (var index = 4; index < 48; index++)
        {
            writer.Payload[index] = (byte)(index * 3);
        }
        var frame = ReadFrame(writer.ToArray());
        Assert.True(LegacyMsgLoginGamePacket.TryParse(frame, out var parsed));
        Assert.Equal(0x12345678U, parsed.Key);
    }

    [Fact]
    public void LegacyMsgLoginGamePacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyMsgLoginGamePacket.Opcode + 1, LegacyMsgLoginGamePacket.PayloadLength);
        writer.WriteUInt32(0, 0x12345678);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyMsgLoginGamePacket.TryParse(frame, out _));
    }

    [Theory]
    [InlineData(LegacyMsgLoginGamePacket.PayloadLength - 1)]
    [InlineData(LegacyMsgLoginGamePacket.PayloadLength + 1)]
    [InlineData(LegacyMsgLoginGamePacket.CapturedClientPayloadLength - 1)]
    [InlineData(LegacyMsgLoginGamePacket.CapturedClientPayloadLength + 1)]
    public void LegacyMsgLoginGamePacketRejectsWrongPayloadLength(int payloadLength)
    {
        var writer = new PacketWriter(LegacyMsgLoginGamePacket.Opcode, payloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyMsgLoginGamePacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
