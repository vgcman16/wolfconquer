using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRelationPacketTests
{
    [Fact]
    public void RelationOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2160, WorldPacketOpcodes.LegacyRelation);
        Assert.Equal(WorldPacketOpcodes.LegacyRelation, LegacyRelationPacket.Opcode);
    }

    [Fact]
    public void RelationPacketRoundTripsSourceTeamRequestShape()
    {
        var packet = LegacyRelationPacket.CreateTeamRequest(
            requesterUid: 1_293_600,
            receiverUid: 400_001,
            requesterLevel: 140,
            requesterBattlePower: 406,
            receiverName: "Target");

        var bytes = packet.ToFrameBytes();

        Assert.Equal(
        [
            0x27, 0x00, 0x70, 0x08,
            0x20, 0xBD, 0x13, 0x00,
            0x81, 0x1A, 0x06, 0x00,
            0x8C, 0x00, 0x00, 0x00,
            0x96, 0x01, 0x00, 0x00,
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0,
            1,
            6, 0,
            (byte)'T', (byte)'a', (byte)'r', (byte)'g', (byte)'e', (byte)'t'
        ],
        bytes);

        Assert.True(LegacyRelationPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void RelationPacketPreservesBooleanFlagsAndUnknownBytes()
    {
        var packet = new LegacyRelationPacket(
            RequesterUid: 1,
            ReceiverUid: 2,
            RequesterLevel: 3,
            RequesterBattlePower: 4,
            SameSpouse: true,
            Friend: true,
            TradePartner: true,
            MentorApprentice: true,
            Teammate: true,
            GuildMember: true,
            Enemy: true,
            Unknown1: 7,
            Unknown2: 8,
            Unknown3: 9,
            ReceiverName: "Receiver");

        Assert.True(LegacyRelationPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void RelationPacketRejectsWrongOpcodeAndInvalidStringList()
    {
        var wrongOpcode = new PacketWriter(
            (ushort)(LegacyRelationPacket.Opcode + 1),
            payloadLength: 29);
        Assert.False(LegacyRelationPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));

        var invalidCount = LegacyRelationPacket.CreateTeamRequest(1, 2, 3, 4, "Name").ToFrameBytes();
        invalidCount[4 + 26] = 2;
        Assert.False(LegacyRelationPacket.TryParse(ReadFrame(invalidCount), out _));

        var invalidLength = LegacyRelationPacket.CreateTeamRequest(1, 2, 3, 4, "Name").ToFrameBytes();
        invalidLength[4 + 27] = 99;
        Assert.False(LegacyRelationPacket.TryParse(ReadFrame(invalidLength), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
