using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClickedUiPacketTests
{
    [Fact]
    public void LegacyItemRefineRecordDefaultResponseUsesLegacyOpcodeAndPoints()
    {
        var packet = LegacyItemRefineRecordPacket.DefaultResponse();
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyItemRefineRecord, frame.Header.Opcode);
        Assert.True(LegacyItemRefineRecordPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.Type);
        Assert.Equal(54U, parsed.Points);
    }

    [Fact]
    public void LegacyGuildListEmptyResponseRoundTrips()
    {
        var packet = LegacyGuildListPacket.Empty();
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyGuildList, frame.Header.Opcode);
        Assert.True(LegacyGuildListPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.Type);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyRankMemberShowViewerEmptyResponseIncludesOneEmptyEntry()
    {
        var packet = LegacyRankMemberShowPacket.EmptyResponse(LegacyRankMemberShowPacket.ViewerType);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyRankMemberShow, frame.Header.Opcode);
        Assert.True(LegacyRankMemberShowPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyRankMemberShowPacket.ViewerType, parsed.Type);
        var entry = Assert.Single(parsed.Items);
        Assert.Equal(0U, entry.EntityUid);
        Assert.Equal(string.Empty, entry.Name);
    }

    [Fact]
    public void LegacyFamilyOccupyQueryParsesAndSourceDefaultInfoResponseMatchesLegacyShape()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyFamilyOccupy, LegacyFamilyOccupyPacket.QueryPayloadLength);
        writer.WriteUInt32(0, (uint)LegacyFamilyOccupyType.ShowInformation);
        writer.WriteUInt32(4, 101907);
        var query = ReadFrame(writer.ToArray());

        Assert.True(LegacyFamilyOccupyPacket.TryParse(query, out var parsed));
        Assert.Equal(LegacyFamilyOccupyType.ShowInformation, parsed.Type);
        Assert.Equal(101907U, parsed.NpcUid);

        var response = ReadFrame(LegacyFamilyOccupyPacket.CreateSourceDefaultShowInformationFrame(parsed.NpcUid));
        Assert.Equal(WorldPacketOpcodes.LegacyFamilyOccupy, response.Header.Opcode);
        Assert.Equal(LegacyFamilyOccupyPacket.ResponsePayloadLength, response.Payload.Length);
        Assert.True(LegacyFamilyOccupyPacket.TryParseShowInformation(response, out var information));
        Assert.Equal(0U, information.ClanId);
        Assert.Equal(101907U, information.NpcUid);
        Assert.Equal(5U, information.Unknown5);
        Assert.Equal("None", information.ClanName);
        Assert.Equal("Phoenix", information.MapName);
        Assert.False(information.Alive);
        Assert.Equal(1, information.UnknownByte);
        Assert.Equal(1, information.UnknownShort);
        Assert.Equal(0U, information.OccupationDays);
        Assert.Equal(0U, information.Reward);
        Assert.Equal(0U, information.NextReward);
    }

    [Theory]
    [InlineData(101888U, "Twin")]
    [InlineData(101890U, "Twin")]
    [InlineData(101905U, "Twin")]
    [InlineData(101907U, "Phoenix")]
    [InlineData(101909U, "Phoenix")]
    [InlineData(101911U, "Ape")]
    [InlineData(101913U, "Ape")]
    [InlineData(101921U, "Desert")]
    [InlineData(101917U, "Desert")]
    [InlineData(101915U, "Bird")]
    [InlineData(101919U, "Bird")]
    public void LegacyFamilyOccupySourceDefaultResponseMapsLegacyNpcIdsToCities(uint npcUid, string city)
    {
        var response = ReadFrame(LegacyFamilyOccupyPacket.CreateSourceDefaultShowInformationFrame(npcUid));

        Assert.True(LegacyFamilyOccupyPacket.TryParseShowInformation(response, out var information));
        Assert.Equal(npcUid, information.NpcUid);
        Assert.Equal(city, information.MapName);
    }

    [Fact]
    public void LegacyInnerPowerOptParsesSourceReaderShape()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyInnerStrengthOpt, LegacyInnerPowerOptPacket.PayloadLength);
        writer.Payload[0] = (byte)LegacyInnerPowerAction.OpenInner;
        BinaryPrimitives.WriteUInt32LittleEndian(writer.Payload.Slice(1, sizeof(uint)), 1293600);
        BinaryPrimitives.WriteUInt32LittleEndian(writer.Payload.Slice(5, sizeof(uint)), 2);

        Assert.True(LegacyInnerPowerOptPacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal(LegacyInnerPowerAction.OpenInner, parsed.Action);
        Assert.Equal(1_293_600U, parsed.Parameter);
        Assert.Equal(2U, parsed.Parameter2);
    }

    [Fact]
    public void LegacyInnerPowerStageInfoRoundTripsSourceWriterShape()
    {
        var packet = new LegacyInnerPowerStageInfoPacket(
            CharacterId: 1_293_600,
            Score: 175,
            LegacyInnerPowerStageInfoAction.UpdateScore,
            [
                new LegacyInnerPowerStageGongInfo(11, 3, 60, Complete: false),
                new LegacyInnerPowerStageGongInfo(12, 5, 100, Complete: true),
            ],
            [
                new LegacyInnerPowerStageAttributeInfo(11, 1, 1200),
                new LegacyInnerPowerStageAttributeInfo(12, 10, 30),
            ]);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(2147, frame.Header.Opcode);
        Assert.Equal(42, frame.Payload.Length);
        Assert.True(LegacyInnerPowerStageInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(1_293_600U, parsed.CharacterId);
        Assert.Equal(175U, parsed.Score);
        Assert.Equal(LegacyInnerPowerStageInfoAction.UpdateScore, parsed.Action);
        Assert.Equal(packet.Gongs, parsed.Gongs);
        Assert.Equal(packet.Attributes, parsed.Attributes);
    }

    [Fact]
    public void LegacyLeagueOptParsesFixedHeaderAndStringList()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyLeagueOpt, payloadLength: 14 + 1 + 2 + 5);
        writer.WriteUInt16(0, 51);
        writer.WriteUInt32(2, 1);
        writer.WriteUInt32(6, 2);
        writer.WriteUInt32(10, 3);
        writer.Payload[14] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(writer.Payload.Slice(15, sizeof(ushort)), 5);
        "Union"u8.CopyTo(writer.Payload[17..]);

        Assert.True(LegacyLeagueOptPacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal(51, parsed.Action);
        Assert.Equal(1U, parsed.Id);
        Assert.Equal(2U, parsed.Uid);
        Assert.Equal(3U, parsed.Index);
        Assert.Equal(["Union"], parsed.Texts);
    }

    [Fact]
    public void LegacyLeagueAllegianceListEmptyResponseUsesSourceHeaderShape()
    {
        var packet = LegacyLeagueAllegianceListPacket.Empty(page: 2);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal((ushort)2032, frame.Header.Opcode);
        Assert.Equal(LegacyLeagueAllegianceListPacket.HeaderPayloadLength, frame.Payload.Length);
        Assert.True(LegacyLeagueAllegianceListPacket.TryParse(frame, out var parsed));
        Assert.Equal(0U, parsed.Count);
        Assert.Equal(2, parsed.Page);
        Assert.Equal(0, parsed.PageCount);
        Assert.Equal(1, parsed.DwParam);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacyLeagueAllegianceListWritesSourceEntryShape()
    {
        var packet = LegacyLeagueAllegianceListPacket.Create(
            count: 4,
            page: 1,
            entries:
            [
                new LegacyLeagueAllegianceEntry(
                    Fund: 123_456,
                    Uid: 1084,
                    GoldBricks: 33,
                    Name: "UnbeatableArmy",
                    LeaderName: "~Super.Shiro~",
                    RecruitDeclaration: "Join us")
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(
            LegacyLeagueAllegianceListPacket.HeaderPayloadLength + LegacyLeagueAllegianceListPacket.EntryPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyLeagueAllegianceListPacket.TryParse(frame, out var parsed));
        Assert.Equal(4U, parsed.Count);
        Assert.Equal(1, parsed.Page);
        Assert.Equal(1, parsed.PageCount);
        Assert.Equal(1, parsed.DwParam);
        var entry = Assert.Single(parsed.Entries);
        Assert.Equal(123_456UL, entry.Fund);
        Assert.Equal(1084U, entry.Uid);
        Assert.Equal(33U, entry.GoldBricks);
        Assert.Equal("UnbeatableArmy", entry.Name);
        Assert.Equal("~Super.Shiro~", entry.LeaderName);
        Assert.Equal("Join us", entry.RecruitDeclaration);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
