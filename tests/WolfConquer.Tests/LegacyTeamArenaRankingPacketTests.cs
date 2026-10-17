using System.Buffers;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamArenaRankingPacketTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-team-arena-ranking-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public LegacyTeamArenaRankingPacketTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Fact]
    public void LegacyTeamArenaOpcodesMatchLegacyWorldOpcodes()
    {
        Assert.Equal((ushort)2186, WorldPacketOpcodes.LegacyTeamArenaRanking);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaRanking, LegacyTeamArenaRankingPacket.Opcode);
        Assert.Equal((ushort)2287, WorldPacketOpcodes.LegacyTeamArenaRank10);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaRank10, LegacyTeamArenaRank10Packet.Opcode);
    }

    [Fact]
    public void LegacyTeamArenaRankingQueryRoundTripsPage()
    {
        var query = LegacyTeamArenaRankingPacket.Query(page: 2);
        var frame = ReadFrame(query.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaRanking, frame.Header.Opcode);
        Assert.Equal(LegacyTeamArenaRankingPacket.QueryPayloadLength, frame.Payload.Length);
        Assert.True(LegacyTeamArenaRankingPacket.TryParseQuery(frame, out var parsed));
        Assert.Equal((ushort)2, parsed.Page);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacyTeamArenaRankingResponseRoundTripsSourceShapedRows()
    {
        var packet = new LegacyTeamArenaRankingPacket(
            Page: 1,
            Count: 2,
            LegacyTeamArenaRankingPacket.ShowPlayerRankListArenaId,
            [
                new LegacyTeamArenaRankingEntry(
                    Rank: 1,
                    LegacyTeamArenaRankingPacket.LegacyUnknown,
                    Points: 4500,
                    Class: 7051,
                    Level: 140,
                    Gender: 0,
                    Name: "HeroOne",
                    ReservedByte: 0,
                    Reserved: 0),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaRanking, frame.Header.Opcode);
        Assert.Equal(
            LegacyTeamArenaRankingPacket.EntriesOffset + LegacyTeamArenaRankingPacket.EntryPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyTeamArenaRankingPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Page, parsed.Page);
        Assert.Equal(packet.Count, parsed.Count);
        Assert.Equal(packet.ArenaId, parsed.ArenaId);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public void LegacyTeamArenaRankingPageUsesPointsOrderGenderAndTenRows()
    {
        var packet = LegacyTeamArenaRankingPacket.CreatePage(
            page: 1,
            Enumerable.Range(1, 11)
                .Select(index => TeamArenaRecord(
                    playerId: 1000 + index,
                    name: "TeamPlayer" + index,
                    level: 120 + index,
                    @class: 7000 + index,
                    mesh: 1541000 + index,
                    points: 4000 + index))
                .ToArray());

        Assert.Equal((ushort)11, packet.Count);
        Assert.Equal(10, packet.Entries.Count);
        Assert.Equal(1U, packet.Entries[0].Rank);
        Assert.Equal("TeamPlayer11", packet.Entries[0].Name);
        Assert.Equal(4011U, packet.Entries[0].Points);
        Assert.Equal(7011U, packet.Entries[0].Class);
        Assert.Equal(131U, packet.Entries[0].Level);
        Assert.Equal((byte)1, packet.Entries[0].Gender);
        Assert.Equal(LegacyTeamArenaRankingPacket.LegacyUnknown, packet.Entries[0].Unknown);
        Assert.All(packet.Entries, entry =>
        {
            Assert.Equal((byte)0, entry.ReservedByte);
            Assert.Equal((ushort)0, entry.Reserved);
        });
    }

    [Fact]
    public async Task HandlerRepliesToTeamArenaRankingQueryFromSeededRecords()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", 140, 7051, 1541008, points: 4500),
                TeamArenaRecord(400001, "Higher", 141, 7052, 1541009, points: 6000),
            ]));
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(LegacyTeamArenaRankingPacket.Query(page: 1).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyTeamArenaRankingPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal((ushort)2, response.Count);
        Assert.Equal(2, response.Entries.Count);
        Assert.Equal("Higher", response.Entries[0].Name);
        Assert.Equal(6000U, response.Entries[0].Points);
        Assert.Equal((byte)0, response.Entries[0].Gender);
        Assert.Equal("HeroOne", response.Entries[1].Name);
        Assert.Equal(4500U, response.Entries[1].Points);
    }

    [Fact]
    public async Task HandlerRepliesWithEmptyTeamArenaRankingPageWhenNoRecordsAreLoaded()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(TeamArenaStatisticsProvider.Empty);
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(LegacyTeamArenaRankingPacket.Query(page: 1).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyTeamArenaRankingPacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal((ushort)0, response.Count);
        Assert.Equal(LegacyTeamArenaRankingPacket.ShowPlayerRankListArenaId, response.ArenaId);
        Assert.Empty(response.Entries);
    }

    [Fact]
    public void LegacyTeamArenaRank10ResponseRoundTripsSourceShapedRows()
    {
        var packet = new LegacyTeamArenaRank10Packet(
            LegacyTeamArenaRank10Packet.ShowPlayerRankListArenaId,
            [
                new LegacyTeamArenaRank10Entry(
                    Name: "HeroOne",
                    LastSeasonRank: 3,
                    Mesh: 1541008,
                    Class: 7051,
                    Level: 140,
                    LastSeasonArenaPoints: 4500,
                    LastSeasonWin: 12,
                    LastSeasonLose: 4),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyTeamArenaRank10, frame.Header.Opcode);
        Assert.Equal(
            LegacyTeamArenaRank10Packet.EntriesOffset + LegacyTeamArenaRank10Packet.EntryPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyTeamArenaRank10Packet.TryParse(frame, out var parsed));
        Assert.Equal(packet.ArenaId, parsed.ArenaId);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public void LegacyTeamArenaRank10UsesLastSeasonArenaPointsOrderAndCapsAtTenRows()
    {
        var packet = LegacyTeamArenaRank10Packet.Create(
            Enumerable.Range(1, 11)
                .Select(index => TeamArenaRecord(
                    playerId: 1000 + index,
                    name: "TeamPlayer" + index,
                    level: 120 + index,
                    @class: 7000 + index,
                    mesh: 1541000 + index,
                    points: 4000,
                    lastSeasonArenaPoints: 6000 + index,
                    lastSeasonWin: 20 + index,
                    lastSeasonLose: 5 + index,
                    lastSeasonRank: 50 + index))
                .ToArray());

        Assert.Equal(10, packet.Entries.Count);
        Assert.Equal("TeamPlayer11", packet.Entries[0].Name);
        Assert.Equal(6011U, packet.Entries[0].LastSeasonArenaPoints);
        Assert.Equal(31U, packet.Entries[0].LastSeasonWin);
        Assert.Equal(16U, packet.Entries[0].LastSeasonLose);
        Assert.Equal(61U, packet.Entries[0].LastSeasonRank);
        Assert.Equal(7011U, packet.Entries[0].Class);
        Assert.Equal(1541011U, packet.Entries[0].Mesh);
    }

    [Fact]
    public async Task HandlerRepliesToTeamArenaRank10QueryFromSeededRecords()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new TeamArenaStatisticsProvider(
            [
                TeamArenaRecord(1293600, "HeroOne", 140, 7051, 1541008, points: 4500, lastSeasonArenaPoints: 5000, lastSeasonWin: 10, lastSeasonLose: 2, lastSeasonRank: 4),
                TeamArenaRecord(400001, "Higher", 141, 7052, 1541009, points: 6000, lastSeasonArenaPoints: 7000, lastSeasonWin: 12, lastSeasonLose: 1, lastSeasonRank: 2),
            ]));
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyTeamArenaRank10Packet.QueryFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyTeamArenaRank10Packet.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(2, response.Entries.Count);
        Assert.Equal("Higher", response.Entries[0].Name);
        Assert.Equal(7000U, response.Entries[0].LastSeasonArenaPoints);
        Assert.Equal("HeroOne", response.Entries[1].Name);
        Assert.Equal(5000U, response.Entries[1].LastSeasonArenaPoints);
    }

    [Fact]
    public async Task HandlerRepliesWithEmptyTeamArenaRank10WhenNoRecordsAreLoaded()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(TeamArenaStatisticsProvider.Empty);
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyTeamArenaRank10Packet.QueryFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyTeamArenaRank10Packet.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(LegacyTeamArenaRank10Packet.ShowPlayerRankListArenaId, response.ArenaId);
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
        LoginLegacyWorldAsync(TeamArenaStatisticsProvider teamArenaStatistics)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoff,
            ArenaStatisticsProvider.Empty,
            teamArenaStatistics);
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

    private static TeamArenaRecord TeamArenaRecord(
        int playerId,
        string name,
        int level,
        int @class,
        int mesh,
        int points,
        int currentHonor = 0,
        int historyHonor = 0,
        int todayBattles = 0,
        int todayWin = 0,
        int totalLose = 0,
        int totalWin = 0,
        int lastSeasonArenaPoints = 0,
        int lastSeasonWin = 0,
        int lastSeasonLose = 0,
        int lastSeasonRank = 0)
    {
        return new TeamArenaRecord(
            playerId,
            name,
            [level, @class, mesh, points, currentHonor, historyHonor, todayBattles, todayWin, totalLose, totalWin, lastSeasonArenaPoints, lastSeasonWin, lastSeasonLose, lastSeasonRank],
            "TeamArena.ini",
            playerId,
            $"{playerId}/{name}/{level}/{@class}/{mesh}/{points}/{currentHonor}/{historyHonor}/{todayBattles}/{todayWin}/{totalLose}/{totalWin}/{lastSeasonArenaPoints}/{lastSeasonWin}/{lastSeasonLose}/{lastSeasonRank}/",
            [
                playerId.ToString(),
                name,
                level.ToString(),
                @class.ToString(),
                mesh.ToString(),
                points.ToString(),
                currentHonor.ToString(),
                historyHonor.ToString(),
                todayBattles.ToString(),
                todayWin.ToString(),
                totalLose.ToString(),
                totalWin.ToString(),
                lastSeasonArenaPoints.ToString(),
                lastSeasonWin.ToString(),
                lastSeasonLose.ToString(),
                lastSeasonRank.ToString(),
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
