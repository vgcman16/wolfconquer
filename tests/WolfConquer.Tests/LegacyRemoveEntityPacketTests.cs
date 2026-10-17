using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRemoveEntityPacketTests
{
    [Fact]
    public void LegacyRemoveEntityOpcodeMatchesLegacyActionFinalize()
    {
        Assert.Equal(WorldPacketOpcodes.MsgAction, LegacyRemoveEntityPacket.Opcode);
        Assert.Equal((ushort)2046, LegacyRemoveEntityPacket.Opcode);
    }

    [Fact]
    public void LegacyRemoveEntityPacketWritesSourceActionQueryShape()
    {
        var packet = LegacyRemoveEntityPacket.FromEntity(1_293_600);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyRemoveEntityPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x08, 0xA0, 0xFA, 0x4E,
            0x48, 0x00,
            0x60, 0x87, 0x01,
            0x68, 0x00,
            0x70, 0x00,
            0x78, 0x00,
        ], frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyRemoveEntityPacketRoundTripsSourceShape()
    {
        var packet = new LegacyRemoveEntityPacket(700_101);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyRemoveEntityPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.Uid, parsed.Uid);
        Assert.Equal(0U, parsed.Timestamp);
        Assert.Equal(0, parsed.Facing);
        Assert.Equal(0U, parsed.PositionX);
        Assert.Equal(0U, parsed.PositionY);
    }

    [Fact]
    public void LegacyRemoveEntityPacketRoundTripsPositionedSourceShape()
    {
        var packet = LegacyRemoveEntityPacket.FromPositionedEntity(
            uid: 830_585,
            timestamp: 123_456,
            facing: 6,
            positionX: 830,
            positionY: 585);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyRemoveEntityPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.Uid, parsed.Uid);
        Assert.Equal(packet.Timestamp, parsed.Timestamp);
        Assert.Equal(packet.Facing, parsed.Facing);
        Assert.Equal(packet.PositionX, parsed.PositionX);
        Assert.Equal(packet.PositionY, parsed.PositionY);
    }

    [Fact]
    public void LegacyRemoveEntityPacketExposesUnderlyingActionPacket()
    {
        var packet = LegacyRemoveEntityPacket.FromEntity(700_101);
        var action = packet.ToActionPacket();

        Assert.Equal(700_101U, action.ObjId);
        Assert.Equal(MsgActionType.RemoveEntity, action.Type);
        Assert.Equal(0U, action.Timestamp);
        Assert.Equal(0, action.Facing);
        Assert.Equal(0U, action.PositionX);
        Assert.Equal(0U, action.PositionY);
    }

    [Fact]
    public void LegacyRemoveEntityPacketExposesUnderlyingPositionedActionPacket()
    {
        var packet = LegacyRemoveEntityPacket.FromPositionedEntity(
            uid: 830_585,
            timestamp: 123_456,
            facing: 6,
            positionX: 830,
            positionY: 585);
        var action = packet.ToActionPacket();

        Assert.Equal(830_585U, action.ObjId);
        Assert.Equal(MsgActionType.RemoveEntity, action.Type);
        Assert.Equal(123_456U, action.Timestamp);
        Assert.Equal(6, action.Facing);
        Assert.Equal(830U, action.PositionX);
        Assert.Equal(585U, action.PositionY);
    }

    [Fact]
    public void LegacyRemoveEntityPacketRejectsNonRemoveAction()
    {
        var frame = ReadFrame(new MsgActionPacket
        {
            ObjId = 700_101,
            Type = MsgActionType.MonsterRespawnEffect,
        }.ToFrameBytes());

        Assert.False(LegacyRemoveEntityPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRemoveEntityPacketRejectsNonSourceRemoveActionExtras()
    {
        var frame = ReadFrame(new MsgActionPacket
        {
            ObjId = 700_101,
            Type = MsgActionType.RemoveEntity,
            TargetPositionX = 830,
        }.ToFrameBytes());

        Assert.False(LegacyRemoveEntityPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRemoveEntityPacketRejectsPartialPositionedRemoveAction()
    {
        var frame = ReadFrame(new MsgActionPacket
        {
            ObjId = 700_101,
            Type = MsgActionType.RemoveEntity,
            Timestamp = 123_456,
            PositionX = 830,
        }.ToFrameBytes());

        Assert.False(LegacyRemoveEntityPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRemoveEntityPacketRejectsNonPositionedRemoveMetadata()
    {
        var frame = ReadFrame(new MsgActionPacket
        {
            ObjId = 700_101,
            Type = MsgActionType.RemoveEntity,
            Timestamp = 123_456,
            Facing = 6,
        }.ToFrameBytes());

        Assert.False(LegacyRemoveEntityPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRemoveEntityPacketRejectsRemoveActionStrings()
    {
        var frame = ReadFrame(new MsgActionPacket
        {
            ObjId = 700_101,
            Type = MsgActionType.RemoveEntity,
            Strings = ["unexpected"],
        }.ToFrameBytes());

        Assert.False(LegacyRemoveEntityPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyRemoveEntityPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyRemoveEntityPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyRemoveEntityPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
