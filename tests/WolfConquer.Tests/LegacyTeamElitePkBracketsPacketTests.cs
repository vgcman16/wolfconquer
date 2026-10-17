using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamElitePkBracketsPacketTests
{
    [Fact]
    public void LegacyTeamElitePkBracketsOpcodeMatchesLegacySourcePacket()
    {
        Assert.Equal((ushort)2198, WorldPacketOpcodes.LegacyTeamElitePkBrackets);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamElitePkBrackets, LegacyTeamElitePkBracketsPacket.Opcode);
    }

    [Fact]
    public void LegacyTeamElitePkBracketsRequestHeaderRoundTripsSourceShape()
    {
        var packet = new LegacyTeamElitePkBracketsPacket(
            LegacyTeamElitePkBracketAction.RequestInformation,
            Page: 1,
            PacketNumber: 0,
            MatchCount: 0,
            Group: 2,
            LegacyTeamElitePkBracketGuiType.Top8Ranking,
            TimeLeft: 0,
            PacketNumber2: 0,
            TotalMatches: 0);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyTeamElitePkBracketsPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(
        [
            0x04, 0x00,
            0x01, 0x00,
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x02, 0x00,
            0x00, 0x00,
            0x00, 0x00,
            0x00,
            0x00, 0x00, 0x00, 0x00,
        ], frame.Payload.ToArray());
        Assert.True(LegacyTeamElitePkBracketsPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyTeamElitePkBracketsSourceEchoPreservesRequestHeaderAndClearsPacketNumber2()
    {
        var request = new LegacyTeamElitePkBracketsPacket(
            LegacyTeamElitePkBracketAction.StaticUpdate,
            Page: 2,
            PacketNumber: 3,
            MatchCount: 1,
            Group: 1,
            LegacyTeamElitePkBracketGuiType.Top8Qualifier,
            TimeLeft: 45,
            PacketNumber2: 9,
            TotalMatches: 1);

        var response = LegacyTeamElitePkBracketsPacket.CreateSourceEcho(request);

        Assert.Equal(request.Action, response.Action);
        Assert.Equal(request.Page, response.Page);
        Assert.Equal(request.PacketNumber, response.PacketNumber);
        Assert.Equal(request.MatchCount, response.MatchCount);
        Assert.Equal(request.Group, response.Group);
        Assert.Equal(request.GuiType, response.GuiType);
        Assert.Equal(request.TimeLeft, response.TimeLeft);
        Assert.Equal((byte)0, response.PacketNumber2);
        Assert.Equal(request.TotalMatches, response.TotalMatches);
    }

    [Fact]
    public void LegacyTeamElitePkBracketsMatchRowsRoundTripSourceBuilderShape()
    {
        var packet = new LegacyTeamElitePkBracketsPacket(
            LegacyTeamElitePkBracketAction.StaticUpdate,
            Page: 0,
            PacketNumber: 0,
            MatchCount: 1,
            Group: 2,
            LegacyTeamElitePkBracketGuiType.Top8Qualifier,
            TimeLeft: 60,
            PacketNumber2: 1,
            TotalMatches: 1);
        var match = new LegacyTeamElitePkBracketMatch(
            Id: 0x0102_0304,
            TeamCount: 2,
            Index: 7,
            Status: LegacyTeamElitePkBracketMatchStatus.Watchable,
            Fighters:
            [
                new LegacyTeamElitePkBracketFighter(
                    Uid: 7001,
                    LeaderUid: 9001,
                    LeaderMesh: 100_003,
                    Name: "TeamOne",
                    Status: LegacyTeamElitePkBracketFighterStatus.None),
                new LegacyTeamElitePkBracketFighter(
                    Uid: 7002,
                    LeaderUid: 9002,
                    LeaderMesh: 100_004,
                    Name: "TeamTwo",
                    Status: LegacyTeamElitePkBracketFighterStatus.Fighting),
            ]);

        var frame = ReadFrame(packet.ToFrameBytes([match]));

        Assert.Equal(
            LegacyTeamElitePkBracketsPacket.HeaderPayloadLength + LegacyTeamElitePkBracketsPacket.MatchPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyTeamElitePkBracketsPacket.TryParse(frame, out var parsed, out var matches));
        Assert.Equal(packet, parsed);
        var parsedMatch = Assert.Single(matches);
        Assert.Equal(match.Id, parsedMatch.Id);
        Assert.Equal(match.TeamCount, parsedMatch.TeamCount);
        Assert.Equal(match.Index, parsedMatch.Index);
        Assert.Equal(match.Status, parsedMatch.Status);
        Assert.Equal(3, parsedMatch.Fighters.Count);
        var first = Assert.IsType<LegacyTeamElitePkBracketFighter>(parsedMatch.Fighters[0]);
        Assert.Equal(7001u, first.Uid);
        Assert.Equal(9001u, first.LeaderUid);
        Assert.Equal(100_003u, first.LeaderMesh);
        Assert.Equal("TeamOne", first.Name);
        Assert.Equal(LegacyTeamElitePkBracketFighterStatus.Waiting, first.Status);
        var second = Assert.IsType<LegacyTeamElitePkBracketFighter>(parsedMatch.Fighters[1]);
        Assert.Equal("TeamTwo", second.Name);
        Assert.Equal(LegacyTeamElitePkBracketFighterStatus.Fighting, second.Status);
        Assert.Null(parsedMatch.Fighters[2]);
    }

    [Fact]
    public void LegacyTeamElitePkBracketsRejectsWrongOpcodeAndMalformedLength()
    {
        var wrongOpcode = new PacketWriter(WorldPacketOpcodes.LegacySourceSilent2234, LegacyTeamElitePkBracketsPacket.PayloadLength);
        Assert.False(LegacyTeamElitePkBracketsPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));

        var malformed = new PacketWriter(WorldPacketOpcodes.LegacyTeamElitePkBrackets, payloadLength: 3);
        malformed.Payload[0] = 0x04;
        malformed.Payload[1] = 0x00;
        malformed.Payload[2] = 0x01;
        Assert.False(LegacyTeamElitePkBracketsPacket.TryParse(ReadFrame(malformed.ToArray()), out _));

        var malformedMatchRows = new PacketWriter(
            WorldPacketOpcodes.LegacyTeamElitePkBrackets,
            LegacyTeamElitePkBracketsPacket.HeaderPayloadLength + 1);
        Assert.False(LegacyTeamElitePkBracketsPacket.TryParse(ReadFrame(malformedMatchRows.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
