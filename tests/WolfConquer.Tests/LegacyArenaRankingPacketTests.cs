using System.Buffers;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArenaRankingPacketTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-arena-ranking-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public LegacyArenaRankingPacketTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Fact]
    public void LegacyArenaRankingOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2093, WorldPacketOpcodes.LegacyArenaRanking);
        Assert.Equal(WorldPacketOpcodes.LegacyArenaRanking, LegacyArenaRankingPacket.Opcode);
    }

    [Fact]
    public void LegacyArenaRankingQueryRoundTripsRankTypeAndPage()
    {
        var query = LegacyArenaRankingPacket.Query(LegacyArenaRankingRankType.History, page: 2);
        var frame = ReadFrame(query.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyArenaRanking, frame.Header.Opcode);
        Assert.Equal(LegacyArenaRankingPacket.QueryPayloadLength, frame.Payload.Length);
        Assert.True(LegacyArenaRankingPacket.TryParseQuery(frame, out var parsed));
        Assert.Equal(LegacyArenaRankingRankType.History, parsed.RankType);
        Assert.Equal((ushort)2, parsed.Page);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacyArenaRankingQueryIgnoresTrailingLegacyPayloadBytes()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyArenaRanking, LegacyArenaRankingPacket.QueryPayloadLength + 4);
        writer.WriteUInt16(0, (ushort)LegacyArenaRankingRankType.Today);
        writer.WriteUInt16(2, 3);
        writer.WriteUInt32(4, 0xAABBCCDD);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyArenaRankingPacket.TryParseQuery(frame, out var parsed));
        Assert.Equal(LegacyArenaRankingRankType.Today, parsed.RankType);
        Assert.Equal((ushort)3, parsed.Page);
    }

    [Fact]
    public void LegacyArenaRankingResponseRoundTripsSourceShapedRows()
    {
        var packet = new LegacyArenaRankingPacket(
            LegacyArenaRankingRankType.Today,
            Page: 1,
            TotalCount: 2,
            LegacyArenaRankingPacket.LegacyPageSizeMarker,
            LegacyArenaRankingPacket.ShowPlayerRankListArenaId,
            [
                new LegacyArenaRankingEntry(
                    Rank: 1,
                    Name: "HeroOne",
                    Unknown: 0,
                    Points: 4500,
                    Class: 7051,
                    Level: 140,
                    Unknown2: 1,
                    Mesh: 100001),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyArenaRanking, frame.Header.Opcode);
        Assert.Equal(
            LegacyArenaRankingPacket.EntriesOffset + LegacyArenaRankingPacket.EntryPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyArenaRankingPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.RankType, parsed.RankType);
        Assert.Equal(packet.Page, parsed.Page);
        Assert.Equal(packet.TotalCount, parsed.TotalCount);
        Assert.Equal(packet.PageSizeMarker, parsed.PageSizeMarker);
        Assert.Equal(packet.ArenaId, parsed.ArenaId);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public void LegacyArenaRankingTodayPageUsesPointsOrderAndEightRows()
    {
        var packet = LegacyArenaRankingPacket.CreatePage(
            LegacyArenaRankingRankType.Today,
            page: 1,
            Enumerable.Range(1, 9)
                .Select(index => ArenaRecord(
                    playerId: 1000 + index,
                    name: "Player" + index,
                    level: 120 + index,
                    @class: 7000 + index,
                    mesh: 100000 + index,
                    points: 4000 + index,
                    currentHonor: 200 + index,
                    historyHonor: 300 + index))
                .ToArray());

        Assert.Equal(9U, packet.TotalCount);
        Assert.Equal(8, packet.Entries.Count);
        Assert.Equal((ushort)1, packet.Entries[0].Rank);
        Assert.Equal("Player9", packet.Entries[0].Name);
        Assert.Equal(4009U, packet.Entries[0].Points);
        Assert.Equal(7009U, packet.Entries[0].Class);
        Assert.Equal(129U, packet.Entries[0].Level);
        Assert.Equal(100009U, packet.Entries[0].Mesh);
        Assert.All(packet.Entries, entry =>
        {
            Assert.Equal((ushort)0, entry.Unknown);
            Assert.Equal(1U, entry.Unknown2);
        });
    }

    [Fact]
    public void LegacyArenaRankingHistoryPageUsesCurrentHonorOrderAndHistoryConstants()
    {
        var packet = LegacyArenaRankingPacket.CreatePage(
            LegacyArenaRankingRankType.History,
            page: 1,
            [
                ArenaRecord(1001, "Low", 120, 7051, 100001, points: 6000, currentHonor: 200, historyHonor: 900),
                ArenaRecord(1002, "High", 121, 7052, 100002, points: 5000, currentHonor: 800, historyHonor: 700),
            ]);

        Assert.Equal(2U, packet.TotalCount);
        Assert.Equal("High", packet.Entries[0].Name);
        Assert.Equal(700U, packet.Entries[0].Points);
        Assert.Equal(LegacyArenaRankingPacket.HistoryUnknown, packet.Entries[0].Unknown);
        Assert.Equal(LegacyArenaRankingPacket.HistoryUnknown2, packet.Entries[0].Unknown2);
        Assert.Equal(7052U, packet.Entries[0].Class);
        Assert.Equal(100002U, packet.Entries[0].Mesh);
    }

    [Fact]
    public async Task HandlerRepliesToArenaRankingQueryFromSeededArenaRecords()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new ArenaStatisticsProvider(
            [
                ArenaRecord(1293600, "HeroOne", 140, 7051, 100001, points: 4500, currentHonor: 500, historyHonor: 600),
                ArenaRecord(400001, "Higher", 141, 7052, 100002, points: 6000, currentHonor: 700, historyHonor: 900),
            ]));
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(LegacyArenaRankingPacket.Query(LegacyArenaRankingRankType.Today, page: 1).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyArenaRankingPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(LegacyArenaRankingRankType.Today, response.RankType);
        Assert.Equal(2U, response.TotalCount);
        Assert.Equal(2, response.Entries.Count);
        Assert.Equal("Higher", response.Entries[0].Name);
        Assert.Equal(6000U, response.Entries[0].Points);
        Assert.Equal("HeroOne", response.Entries[1].Name);
        Assert.Equal(4500U, response.Entries[1].Points);
    }

    [Fact]
    public async Task HandlerRepliesWithEmptyArenaRankingPageWhenNoArenaRecordsAreLoaded()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(ArenaStatisticsProvider.Empty);
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(LegacyArenaRankingPacket.Query(LegacyArenaRankingRankType.History, page: 1).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyArenaRankingPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(LegacyArenaRankingRankType.History, response.RankType);
        Assert.Equal(0U, response.TotalCount);
        Assert.Equal(LegacyArenaRankingPacket.LegacyPageSizeMarker, response.PageSizeMarker);
        Assert.Equal(LegacyArenaRankingPacket.ShowPlayerRankListArenaId, response.ArenaId);
        Assert.Empty(response.Entries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginLegacyWorldAsync(ArenaStatisticsProvider arenaStatistics)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            arenaStatistics);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WriteSnapshot(long accountId, long uid)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            GuildDontion=900
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            Activeness=0/80/2/1/10/1/22/0/0/

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }

    private static ArenaRecord ArenaRecord(
        int playerId,
        string name,
        int level,
        int @class,
        int mesh,
        int points,
        int currentHonor,
        int historyHonor)
    {
        return new ArenaRecord(
            playerId,
            name,
            [level, @class, mesh, points, currentHonor, historyHonor, 0, 0, 0, 0, 0, 0, 4000, 0, 0, 1],
            "Arena.ini",
            playerId,
            $"{playerId}/{name}/{level}/{@class}/{mesh}/{points}/{currentHonor}/{historyHonor}/0/0/0/0/4000/0/0/1/",
            [
                playerId.ToString(),
                name,
                level.ToString(),
                @class.ToString(),
                mesh.ToString(),
                points.ToString(),
                currentHonor.ToString(),
                historyHonor.ToString(),
                "0",
                "0",
                "0",
                "0",
                "4000",
                "0",
                "0",
                "1",
                "",
            ]);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }
}
