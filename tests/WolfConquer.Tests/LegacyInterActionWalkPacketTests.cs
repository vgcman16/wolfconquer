using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyInterActionWalkPacketTests
{
    [Fact]
    public void LegacyInterActionWalkOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2141, WorldPacketOpcodes.LegacyInterActionWalk);
        Assert.Equal(WorldPacketOpcodes.LegacyInterActionWalk, LegacyInterActionWalkPacket.Opcode);
    }

    [Fact]
    public void LegacyInterActionWalkForWalkWritesLegacyStructLayout()
    {
        var packet = LegacyInterActionWalkPacket.ForWalk(
            uid: 0x01020304,
            opponentUid: 0xA0B0C0D0,
            directionOne: 5,
            directionTwo: 7);

        Assert.Equal(
        [
            0x18, 0x00, 0x5D, 0x08,
            0x01, 0x00,
            0x05,
            0x07,
            0x00, 0x00,
            0x00, 0x00,
            0x02, 0x00, 0x00, 0x00,
            0x04, 0x03, 0x02, 0x01,
            0xD0, 0xC0, 0xB0, 0xA0
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyInterActionWalkForJumpPacksTargetCoordinatesIntoLegacyStructLayout()
    {
        var packet = LegacyInterActionWalkPacket.ForJump(
            uid: 0x01020304,
            opponentUid: 0xA0B0C0D0,
            x: 840,
            y: 590);

        Assert.Equal(
        [
            0x18, 0x00, 0x5D, 0x08,
            0x02, 0x00,
            0x48,
            0x03,
            0x4E, 0x02,
            0x00, 0x00,
            0x02, 0x00, 0x00, 0x00,
            0x04, 0x03, 0x02, 0x01,
            0xD0, 0xC0, 0xB0, 0xA0
        ],
        packet.ToFrameBytes());
        Assert.Equal(840, packet.X);
        Assert.Equal(590, packet.Y);
    }

    [Fact]
    public void LegacyInterActionWalkPacketRoundTripsFields()
    {
        var packet = LegacyInterActionWalkPacket.ForWalk(
            uid: 1_293_600,
            opponentUid: 1_293_601,
            directionOne: 11);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyInterActionWalkPacket.TryParse(frame, out var parsed));

        Assert.Equal(LegacyInterActionWalkMode.Walk, parsed.Mode);
        Assert.Equal(11, parsed.DirectionOne);
        Assert.Equal(0, parsed.DirectionTwo);
        Assert.Equal(0, parsed.Y);
        Assert.Equal(0, parsed.Unknown);
        Assert.Equal(2U, parsed.Count);
        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(1_293_601U, parsed.OpponentUid);
    }

    [Fact]
    public void LegacyInterActionWalkPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyInterActionWalkPacket.Opcode + 1, LegacyInterActionWalkPacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyInterActionWalkPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyInterActionWalkPacketRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyInterActionWalkPacket.Opcode, LegacyInterActionWalkPacket.PayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyInterActionWalkPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyInterActionWalkPacketRejectsInvalidMode()
    {
        var writer = new PacketWriter(LegacyInterActionWalkPacket.Opcode, LegacyInterActionWalkPacket.PayloadLength);
        writer.WriteUInt16(0, 99);
        writer.WriteUInt32(8, 2);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyInterActionWalkPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyInterActionWalkPacketRejectsCountOtherThanTwo()
    {
        var writer = new PacketWriter(LegacyInterActionWalkPacket.Opcode, LegacyInterActionWalkPacket.PayloadLength);
        writer.WriteUInt16(0, (ushort)LegacyInterActionWalkMode.Walk);
        writer.WriteUInt32(8, 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyInterActionWalkPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
