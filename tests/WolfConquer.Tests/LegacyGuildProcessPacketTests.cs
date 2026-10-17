using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildProcessPacketTests
{
    [Fact]
    public void OpcodeMatchesLegacyMsgGuildProcesFinalizeValue()
    {
        Assert.Equal((ushort)2167, WorldPacketOpcodes.LegacyGuildProcess);
        Assert.Equal(WorldPacketOpcodes.LegacyGuildProcess, LegacyGuildProcessPacket.Opcode);
    }

    [Fact]
    public void SyntheticInfoNameRequestParsesLegacyLayout()
    {
        var bytes = new byte[32];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0, 2), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2, 2), 2167);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8, 4), 0x10203040);
        var frame = ReadFrame(bytes);

        Assert.Equal(32, frame.Header.Length);
        Assert.Equal(LegacyGuildProcessPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(28, frame.Payload.Length);
        Assert.True(LegacyGuildProcessPacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyGuildProcessAction.InfoName, packet.Action);
        Assert.Equal(0x10203040U, packet.Uid);
        Assert.Equal(0, packet.Argument0);
        Assert.Equal(0, packet.Argument1);
        Assert.Equal(0, packet.Argument2);
        Assert.Equal(0, packet.Argument3);
        Assert.Empty(packet.Strings);
        Assert.Equal(bytes, packet.ToFrameBytes());
    }

    [Fact]
    public void PacketRoundTripsStringListAndTrailingZeros()
    {
        var packet = LegacyGuildProcessPacket.Create(
            LegacyGuildProcessAction.Bulletin,
            uid: 1234,
            args: [-1, 2, 3, 4],
            strings: ["Alpha", "Beta Guild", string.Empty]);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);
        var payload = frame.Payload.ToArray();

        Assert.Equal(LegacyGuildProcessPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(3, payload[24]);
        Assert.Equal([0, 0, 0], payload[^3..]);
        Assert.True(LegacyGuildProcessPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyGuildProcessAction.Bulletin, parsed.Action);
        Assert.Equal(1234U, parsed.Uid);
        Assert.Equal(-1, parsed.Argument0);
        Assert.Equal(2, parsed.Argument1);
        Assert.Equal(3, parsed.Argument2);
        Assert.Equal(4, parsed.Argument3);
        Assert.Equal(["Alpha", "Beta Guild", string.Empty], parsed.Strings);
    }

    [Fact]
    public void GuildInformationPacketRoundTripsSourceBuilderLayout()
    {
        var packet = new LegacyGuildInformationPacket(
            GuildId: 0x10203040,
            SilverFund: 12_345_678_901,
            ConquerPointFund: 123_456,
            MemberCount: 10,
            MyRank: 6,
            LeaderName: "TestLeader",
            Level: 9,
            CreateTime: 20240102,
            LeaderId: 9_000_001,
            Material: 7,
            RecruitmentOff: true,
            RecruitmentBattlePower: 325,
            RecruitmentOn: false,
            Prestige: 55,
            RecruitmentFlag: 0x3F);

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal((ushort)2172, LegacyGuildInformationPacket.Opcode);
        Assert.Equal(LegacyGuildInformationPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(4 + LegacyGuildInformationPacket.PayloadLength, frame.Header.Length);
        Assert.Equal(93, frame.Payload.Length);
        Assert.Equal(0, frame.Payload.ToArray()[58]);
        Assert.True(LegacyGuildInformationPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void GuildInformationPacketTruncatesFixedLeaderNameLikeSourcePacket()
    {
        var packet = new LegacyGuildInformationPacket(
            GuildId: 1,
            SilverFund: 0,
            ConquerPointFund: 0,
            MemberCount: 1,
            MyRank: 1,
            LeaderName: "abcdefghijklmnopqrstuvwxyz1234567890",
            Level: 1,
            CreateTime: 0,
            LeaderId: 1,
            Material: 0,
            RecruitmentOff: false,
            RecruitmentBattlePower: 0,
            RecruitmentOn: true,
            Prestige: 0,
            RecruitmentFlag: 0);

        Assert.True(LegacyGuildInformationPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal("abcdefghijklmnopqrstuvwxyz123456", parsed.LeaderName);
    }

    [Fact]
    public void TryParseAcceptsUnknownActionForLegacySwitchDefault()
    {
        var writer = new PacketWriter(LegacyGuildProcessPacket.Opcode, LegacyGuildProcessPacket.MinimumPayloadLength);
        writer.WriteUInt32(0, 999);

        Assert.True(LegacyGuildProcessPacket.TryParse(ReadFrame(writer.ToArray()), out var packet));
        Assert.Equal((LegacyGuildProcessAction)999U, packet.Action);
    }

    [Fact]
    public void TryParseRejectsMalformedStringListButAcceptsLegacyTrailerBytes()
    {
        var malformedStringList = new PacketWriter(
            LegacyGuildProcessPacket.Opcode,
            LegacyGuildProcessPacket.MinimumPayloadLength + sizeof(ushort));
        malformedStringList.WriteUInt32(0, (uint)LegacyGuildProcessAction.InfoName);
        malformedStringList.Payload[24] = 1;
        malformedStringList.WriteUInt16(25, 4);

        Assert.False(LegacyGuildProcessPacket.TryParse(ReadFrame(malformedStringList.ToArray()), out _));

        var nonZeroTrailer = LegacyGuildProcessPacket.Create(LegacyGuildProcessAction.InfoName, uid: 0x10203040).ToFrameBytes();
        nonZeroTrailer[^1] = 1;

        Assert.True(LegacyGuildProcessPacket.TryParse(ReadFrame(nonZeroTrailer), out var parsed));
        Assert.Equal(LegacyGuildProcessAction.InfoName, parsed.Action);
        Assert.Equal(0x10203040U, parsed.Uid);
    }

    [Fact]
    public void CreateRejectsMoreThanFourArgumentSlots()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyGuildProcessPacket.Create(
                LegacyGuildProcessAction.Show,
                uid: 1,
                args: [1, 2, 3, 4, 5]));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
