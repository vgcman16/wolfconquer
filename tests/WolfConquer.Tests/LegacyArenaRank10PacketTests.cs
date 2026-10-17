using System.Buffers;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArenaRank10PacketTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-arena-rank10-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public LegacyArenaRank10PacketTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Fact]
    public void LegacyArenaRank10OpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2293, WorldPacketOpcodes.LegacyArenaRank10);
        Assert.Equal(WorldPacketOpcodes.LegacyArenaRank10, LegacyArenaRank10Packet.Opcode);
    }

    [Fact]
    public void LegacyArenaRank10ResponseRoundTripsSourceShapedRows()
    {
        var packet = new LegacyArenaRank10Packet(
            LegacyArenaRank10Packet.ShowPlayerRankListArenaId,
            LegacyArenaRank10Packet.LegacyMarker,
            [
                new LegacyArenaRank10Entry(
                    Uid: 1_293_600,
                    Name: "HeroOne",
                    Mesh: 100001,
                    Level: 140,
                    Class: 7051,
                    LastSeasonRank: 3,
                    LastSeasonRankMirror: 3,
                    LastSeasonArenaPoints: 4500,
                    LastSeasonWin: 12,
                    LastSeasonLose: 4),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyArenaRank10, frame.Header.Opcode);
        Assert.Equal(
            LegacyArenaRank10Packet.EntriesOffset + LegacyArenaRank10Packet.EntryPayloadLength,
            frame.Payload.Length);
        Assert.True(LegacyArenaRank10Packet.TryParse(frame, out var parsed));
        Assert.Equal(packet.ArenaId, parsed.ArenaId);
        Assert.Equal(packet.Marker, parsed.Marker);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public void LegacyArenaRank10UsesLastSeasonArenaPointsOrderAndCapsAtTenRows()
    {
        var packet = LegacyArenaRank10Packet.Create(
            Enumerable.Range(1, 11)
                .Select(index => ArenaRecord(
                    playerId: 1000 + index,
                    name: "Player" + index,
                    level: 120 + index,
                    @class: 7000 + index,
                    mesh: 100000 + index,
                    currentPoints: 4000,
                    lastSeasonArenaPoints: 6000 + index,
                    lastSeasonWin: 20 + index,
                    lastSeasonLose: 5 + index,
                    lastSeasonRank: 50 + index))
                .ToArray());

        Assert.Equal(10, packet.Entries.Count);
        Assert.Equal("Player11", packet.Entries[0].Name);
        Assert.Equal(6011U, packet.Entries[0].LastSeasonArenaPoints);
        Assert.Equal(31U, packet.Entries[0].LastSeasonWin);
        Assert.Equal(16U, packet.Entries[0].LastSeasonLose);
        Assert.Equal(61U, packet.Entries[0].LastSeasonRank);
        Assert.Equal(packet.Entries[0].LastSeasonRank, packet.Entries[0].LastSeasonRankMirror);
        Assert.Equal(7011U, packet.Entries[0].Class);
        Assert.Equal(100011U, packet.Entries[0].Mesh);
    }

    [Fact]
    public async Task HandlerRepliesToArenaRank10QueryFromSeededArenaRecords()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            new ArenaStatisticsProvider(
            [
                ArenaRecord(1293600, "HeroOne", 140, 7051, 100001, currentPoints: 4500, lastSeasonArenaPoints: 5000, lastSeasonWin: 10, lastSeasonLose: 2, lastSeasonRank: 4),
                ArenaRecord(400001, "Higher", 141, 7052, 100002, currentPoints: 6000, lastSeasonArenaPoints: 7000, lastSeasonWin: 12, lastSeasonLose: 1, lastSeasonRank: 2),
            ]));
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyArenaRank10Packet.QueryFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyArenaRank10Packet.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(2, response.Entries.Count);
        Assert.Equal("Higher", response.Entries[0].Name);
        Assert.Equal(7000U, response.Entries[0].LastSeasonArenaPoints);
        Assert.Equal("HeroOne", response.Entries[1].Name);
        Assert.Equal(5000U, response.Entries[1].LastSeasonArenaPoints);
    }

    [Fact]
    public async Task HandlerRepliesWithEmptyArenaRank10WhenNoArenaRecordsAreLoaded()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(ArenaStatisticsProvider.Empty);
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyArenaRank10Packet.QueryFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyArenaRank10Packet.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(LegacyArenaRank10Packet.ShowPlayerRankListArenaId, response.ArenaId);
        Assert.Equal(LegacyArenaRank10Packet.LegacyMarker, response.Marker);
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
        int currentPoints,
        int lastSeasonArenaPoints,
        int lastSeasonWin,
        int lastSeasonLose,
        int lastSeasonRank)
    {
        return new ArenaRecord(
            playerId,
            name,
            [level, @class, mesh, currentPoints, 0, 0, 0, 0, 0, 0, 0, 0, lastSeasonArenaPoints, lastSeasonWin, lastSeasonLose, lastSeasonRank],
            "Arena.ini",
            playerId,
            $"{playerId}/{name}/{level}/{@class}/{mesh}/{currentPoints}/0/0/0/0/0/0/{lastSeasonArenaPoints}/{lastSeasonWin}/{lastSeasonLose}/{lastSeasonRank}/",
            [
                playerId.ToString(),
                name,
                level.ToString(),
                @class.ToString(),
                mesh.ToString(),
                currentPoints.ToString(),
                "0",
                "0",
                "0",
                "0",
                "0",
                "0",
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
