using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGenericRankingPacketTests
{
    private static LegacyChiPowerState CreatePower(LegacyChiPowerType type) => new(
        type, Unlocked: true, Experience: 12,
        Attributes:
        [
            new LegacyChiAttributeState(1, 200, false),
            new LegacyChiAttributeState(2, 105, false),
            new LegacyChiAttributeState(3, 10, false),
            new LegacyChiAttributeState(4, 200, false),
        ]);

    [Fact]
    public void LegacyGenericRankingOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2165, WorldPacketOpcodes.LegacyGenericRanking);
        Assert.Equal(WorldPacketOpcodes.LegacyGenericRanking, LegacyGenericRankingPacket.Opcode);
    }

    [Fact]
    public void LegacyGenericRankingActionValuesMatchLegacyMsgGenericRanking()
    {
        Assert.Equal((byte)1, (byte)LegacyGenericRankingAction.Ranking);
        Assert.Equal((byte)2, (byte)LegacyGenericRankingAction.QueryCount);
        Assert.Equal((byte)4, (byte)LegacyGenericRankingAction.UpdateScreen);
        Assert.Equal((byte)5, (byte)LegacyGenericRankingAction.InformationRequest);
        Assert.Equal((byte)6, (byte)LegacyGenericRankingAction.PrestigeRanks);
    }

    [Fact]
    public void LegacyGenericRankingPacketRoundTripsSourceSilentHeaderActions()
    {
        foreach (var action in new[]
        {
            LegacyGenericRankingAction.UpdateScreen,
            LegacyGenericRankingAction.InformationRequest,
        })
        {
            var rankType = (LegacyGenericRankingRankType)70_000_000u;
            var packet = new LegacyGenericRankingPacket(
                action,
                rankType,
                RegisteredCount: 3,
                Page: 4,
                Finish: 0,
                Count: 0,
                Reserved: 0,
                Entries: []);
            var frame = ReadFrame(packet.ToFrameBytes());

            Assert.Equal(LegacyGenericRankingPacket.PayloadLengthForEntryCount(0), frame.Payload.Length);
            Assert.True(LegacyGenericRankingPacket.TryParse(frame, out var parsed));
            Assert.Equal(action, parsed.Action);
            Assert.Equal(rankType, parsed.RankType);
            Assert.Equal(3, parsed.RegisteredCount);
            Assert.Equal(4, parsed.Page);
            Assert.Equal(0, parsed.Finish);
            Assert.Equal(0, parsed.Count);
            Assert.Empty(parsed.Entries);
        }
    }

    [Fact]
    public void LegacyChiQueryCountPacketWritesSingleRankingItem()
    {
        var power = CreatePower(LegacyChiPowerType.Dragon);
        var packet = LegacyGenericRankingPacket.ChiQueryCount(9_000_001, "FixtureHero", power);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyGenericRanking, frame.Header.Opcode);
        Assert.Equal(LegacyGenericRankingPacket.PayloadLengthForEntryCount(1), frame.Payload.Length);
        Assert.True(LegacyGenericRankingPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyGenericRankingAction.QueryCount, parsed.Action);
        Assert.Equal(LegacyGenericRankingRankType.DragonChi, parsed.RankType);
        Assert.Equal(0, parsed.RegisteredCount);
        Assert.Equal(0, parsed.Page);
        Assert.Equal(0, parsed.Finish);
        Assert.Equal(1, parsed.Count);
        Assert.Equal(0U, parsed.Reserved);

        var entry = Assert.Single(parsed.Entries);
        Assert.Equal(1, entry.Rank);
        Assert.Equal(250, entry.Amount);
        Assert.Equal(9_000_001U, entry.Uid);
        Assert.Equal(9_000_001U, entry.MirrorUid);
        Assert.Equal("FixtureHero", entry.Name);
        Assert.Equal("FixtureHero", entry.SecondaryName);
    }

    [Fact]
    public void LegacyChiQueryCountPacketWritesSyntheticFixedWidthNames()
    {
        var packet = LegacyGenericRankingPacket.ChiQueryCount(
            9_000_001, new string('R', 40), CreatePower(LegacyChiPowerType.Dragon));
        var bytes = packet.ToFrameBytes();

        // Construct the complete expected frame from invented inputs and protocol offsets.
        var expected = new byte[130];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), 130);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 2165);
        expected[4] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(5, 4), 60_000_001);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(13, 4), 1);
        expected[21] = 1;
        BinaryPrimitives.WriteInt64LittleEndian(expected.AsSpan(22, 8), 250);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(30, 4), 9_000_001);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(34, 4), 9_000_001);
        Encoding.UTF8.GetBytes(new string('R', 32)).CopyTo(expected, 38);
        Encoding.UTF8.GetBytes(new string('R', 32)).CopyTo(expected, 70);

        Assert.Equal(expected, bytes);
        Assert.True(LegacyGenericRankingPacket.TryParse(ReadFrame(bytes), out var parsed));
        var entry = Assert.Single(parsed.Entries);
        Assert.Equal(new string('R', 32), entry.Name);
        Assert.Equal(new string('R', 32), entry.SecondaryName);
    }

    [Fact]
    public void LegacyChiQueryCountStartupPacketsFollowLegacyPowerOrder()
    {
        var state = new LegacyChiState(
            ChiPoints: 125,
            Powers:
            [
                CreatePower(LegacyChiPowerType.Dragon),
                CreatePower(LegacyChiPowerType.Phoenix),
                CreatePower(LegacyChiPowerType.Tiger),
                CreatePower(LegacyChiPowerType.Turtle),
            ]);

        var packets = LegacyGenericRankingPacket.CreateChiQueryCountStartup(9_000_001, "FixtureHero", state);

        Assert.Equal(
            [LegacyGenericRankingRankType.DragonChi, LegacyGenericRankingRankType.PhoenixChi, LegacyGenericRankingRankType.TigerChi, LegacyGenericRankingRankType.TurtleChi],
            packets.Select(packet => packet.RankType));
        Assert.All(packets, packet =>
        {
            var entry = Assert.Single(packet.Entries);
            Assert.Equal(250, entry.Amount);
            Assert.Equal("FixtureHero", entry.Name);
        });
    }

    [Fact]
    public void LegacyGenericRankingPacketAcceptsLegacyHeaderOnlyQueryRequests()
    {
        var writer = new PacketWriter(LegacyGenericRankingPacket.Opcode, LegacyGenericRankingPacket.PayloadLengthForEntryCount(0));
        writer.Payload[0] = (byte)LegacyGenericRankingAction.QueryCount;
        writer.WriteUInt32(1, (uint)LegacyGenericRankingRankType.DragonChi);
        writer.WriteUInt16(5, 0);
        writer.Payload[7] = 0;
        writer.Payload[8] = 0;
        writer.WriteInt32(9, 1);
        writer.WriteUInt32(13, 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyGenericRankingPacket.TryParse(frame, out var packet));
        Assert.Equal(LegacyGenericRankingAction.QueryCount, packet.Action);
        Assert.Equal(LegacyGenericRankingRankType.DragonChi, packet.RankType);
        Assert.Equal(1, packet.Count);
        Assert.Empty(packet.Entries);
    }

    [Fact]
    public void LegacyGenericRankingEmptyResponseRoundTripsNoDataListShape()
    {
        var rankType = (LegacyGenericRankingRankType)180_000_000u;
        var packet = LegacyGenericRankingPacket.EmptyResponse(LegacyGenericRankingAction.Ranking, rankType, page: 2);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyGenericRankingPacket.PayloadLengthForEntryCount(0), frame.Payload.Length);
        Assert.True(LegacyGenericRankingPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyGenericRankingAction.Ranking, parsed.Action);
        Assert.Equal(rankType, parsed.RankType);
        Assert.Equal(0, parsed.RegisteredCount);
        Assert.Equal(2, parsed.Page);
        Assert.Equal(1, parsed.Finish);
        Assert.Equal(0, parsed.Count);
        Assert.Equal(0U, parsed.Reserved);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacyGenericRankingPacketRejectsPayloadShorterThanLegacyHeader()
    {
        var writer = new PacketWriter(LegacyGenericRankingPacket.Opcode, payloadLength: 12);
        writer.Payload[0] = (byte)LegacyGenericRankingAction.QueryCount;
        writer.WriteUInt32(1, (uint)LegacyGenericRankingRankType.DragonChi);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyGenericRankingPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
