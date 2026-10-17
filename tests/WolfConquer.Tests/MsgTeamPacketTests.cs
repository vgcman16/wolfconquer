using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class MsgTeamPacketTests
{
    [Theory]
    [InlineData(0, MsgTeamType.Create)]
    [InlineData(1, MsgTeamType.JoinRequest)]
    [InlineData(2, MsgTeamType.ExitTeam)]
    [InlineData(3, MsgTeamType.AcceptInvitation)]
    [InlineData(4, MsgTeamType.InviteRequest)]
    [InlineData(5, MsgTeamType.AcceptJoinRequest)]
    [InlineData(6, MsgTeamType.Dismiss)]
    [InlineData(7, MsgTeamType.Kick)]
    [InlineData(8, MsgTeamType.ForbidJoining)]
    [InlineData(9, MsgTeamType.UnforbidJoining)]
    [InlineData(10, MsgTeamType.LootMoneyOff)]
    [InlineData(11, MsgTeamType.LootMoneyOn)]
    [InlineData(12, MsgTeamType.LootItemsOff)]
    [InlineData(13, MsgTeamType.LootItemsOn)]
    [InlineData(14, MsgTeamType.RejectInvitation)]
    [InlineData(15, MsgTeamType.Send)]
    public void MsgTeamTypeDefinesLegacyNumericValues(int expected, MsgTeamType type)
    {
        Assert.Equal((uint)expected, (uint)type);
    }

    [Fact]
    public void MsgTeamOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2243, WorldPacketOpcodes.MsgTeam);
        Assert.Equal(WorldPacketOpcodes.MsgTeam, MsgTeamPacket.Opcode);
    }

    [Fact]
    public void MsgTeamPacketRoundTripsTypeAndUid()
    {
        var packet = new MsgTeamPacket(MsgTeamType.AcceptJoinRequest, 1_293_600);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgTeamPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void MsgTeamPacketIgnoresTrailingLegacyPayloadBytes()
    {
        var writer = new PacketWriter(MsgTeamPacket.Opcode, MsgTeamPacket.PayloadLength + 4);
        writer.WriteUInt32(0, (uint)MsgTeamType.InviteRequest);
        writer.WriteUInt32(4, 1_293_600);
        writer.WriteUInt32(8, 0xAABBCCDD);

        Assert.True(MsgTeamPacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));

        Assert.Equal(MsgTeamType.InviteRequest, parsed.Type);
        Assert.Equal(1_293_600U, parsed.Uid);
    }

    [Fact]
    public void MsgTeamPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(MsgTeamPacket.Opcode + 1, MsgTeamPacket.PayloadLength);
        writer.WriteUInt32(0, (uint)MsgTeamType.Create);
        writer.WriteUInt32(4, 1_293_600);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(MsgTeamPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgTeamPacketRejectsShortPayload()
    {
        var frame = ReadFrame(
        [
            0x0B, 0x00, 0xC3, 0x08,
            0x04, 0x00, 0x00, 0x00,
            0x04, 0x03, 0x02
        ]);

        Assert.False(MsgTeamPacket.TryParse(frame, out _));
    }

    [Fact]
    public void MsgTeamPacketPreservesUnknownTypeValue()
    {
        const uint unknownType = 0xA0B0C0D0;
        var packet = new MsgTeamPacket((MsgTeamType)unknownType, 0x01020304);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(MsgTeamPacket.TryParse(frame, out var parsed));

        Assert.Equal(unknownType, (uint)parsed.Type);
        Assert.Equal(packet.Uid, parsed.Uid);
    }

    [Fact]
    public void MsgTeamPacketWritesLegacyLittleEndianShape()
    {
        var packet = new MsgTeamPacket(MsgTeamType.InviteRequest, 0x01020304);

        Assert.Equal(
        [
            0x0C, 0x00, 0xC3, 0x08,
            0x04, 0x00, 0x00, 0x00,
            0x04, 0x03, 0x02, 0x01
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
