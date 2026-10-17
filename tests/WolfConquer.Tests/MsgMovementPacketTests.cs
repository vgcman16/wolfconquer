using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgMovementPacketTests
{
    [Fact]
    public void MsgMovementOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2205, WorldPacketOpcodes.MsgMovement);
        Assert.Equal(WorldPacketOpcodes.MsgMovement, MsgMovementPacket.Opcode);
    }

    [Fact]
    public void MsgMovementPacketRoundTripsFields()
    {
        var packet = new MsgMovementPacket
        {
            Direction = 5,
            Uid = 1_293_600,
            Running = MsgMovementMode.Steed,
            Timestamp = 1234,
            MapId = 1015,
        };
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgMovementPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void MsgMovementPacketWritesLegacyWalkQueryProtobufShape()
    {
        var packet = new MsgMovementPacket
        {
            Direction = 5,
            Uid = 7,
            Running = MsgMovementMode.Steed,
            Timestamp = 11,
            MapId = 1002,
        };

        Assert.Equal(
        [
            0x0F, 0x00, 0x9D, 0x08,
            0x08, 0x05,
            0x10, 0x07,
            0x18, 0x09,
            0x20, 0x0B,
            0x28, 0xEA, 0x07
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void MsgMovementPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(MsgMovementPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgMovementPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgMovementPacketRejectsMissingRequiredField()
    {
        var writer = new PacketWriter(MsgMovementPacket.Opcode, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x05;
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgMovementPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
