using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySkyTowerFloorThreeProgressionTests
{
    private const int InstanceMapId = 10_429;
    private const int InstanceSourceMapId = 10_428;
    private const uint FloorThreeChallengeId = 478;
    private const uint FloorFourChallengeId = 479;
    private const int FloorThreeMonsterFamilyId = 6_074;
    private const int FloorFourMonsterFamilyId = 6_075;
    private const uint FirstDynamicMonsterUid = 900_000;
    private const uint LastDynamicMonsterUid = 999_999;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task FloorThreeCompletesOnlyAfterThirdHandlerKillThenFloorFourGetsExactFreshObjective()
    {
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            maps: [CreateMapMetadata(InstanceSourceMapId, type: 18_685_966)],
            monsters:
            [
                CreateMonsterFamily(FloorThreeMonsterFamilyId),
                CreateMonsterFamily(FloorFourMonsterFamilyId),
            ]);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            CreateContext(connection, session, worldData, sessions, monsterStates, log),
            ReadFrame(new LegacyInstancePacket(
                    LegacyInstanceAction.Info,
                    weather: FloorThreeChallengeId,
                    userId: checked((uint)session.Character.Uid))
                .ToFrameBytes()));

        var floorThreeSession = GetSession(sessions, connection);
        var floorThreeDynamicId = ReadDynamicId(floorThreeSession);
        Assert.Equal(InstanceMapId, floorThreeSession.Character.MapId);
        Assert.Equal("478", floorThreeSession.Character.Attributes["SkyTowerActiveChallengeId"]);
        Assert.Equal("3", floorThreeSession.Character.Attributes["SkyTowerFloor"]);
        Assert.Equal("477", floorThreeSession.Character.Attributes["SkyTowerCompletedChallengeId"]);
        Assert.Equal("10", floorThreeSession.Character.Attributes["SkyTowerAvailableAttempts"]);
        Assert.False(floorThreeSession.Character.Attributes.ContainsKey("SkyTowerActiveCompletionId"));

        var floorThreeMonsters = monsterStates
            .GetVisibleDynamicMonsters(InstanceMapId, floorThreeDynamicId, DateTimeOffset.UtcNow)
            .OrderBy(static entry => entry.Instance.SpawnIndex)
            .ToArray();
        Assert.Equal(3, floorThreeMonsters.Length);
        Assert.All(floorThreeMonsters, entry =>
        {
            Assert.Equal(InstanceMapId, entry.Instance.MapId);
            Assert.Equal(floorThreeDynamicId, entry.Instance.DynamicId);
            Assert.Equal((uint)FloorThreeMonsterFamilyId, entry.Instance.MonsterId);
            Assert.Equal(FloorThreeMonsterFamilyId, entry.MonsterFamily.Id);
            Assert.InRange(entry.Instance.Uid, FirstDynamicMonsterUid, LastDynamicMonsterUid);
        });
        Assert.Equal(3, floorThreeMonsters.Select(static entry => entry.Instance.Uid).Distinct().Count());
        Assert.Equal(
            new (ushort X, ushort Y)[] { (61, 66), (62, 66), (60, 66) },
            floorThreeMonsters.Select(static entry => (entry.Instance.X, entry.Instance.Y)).ToArray());
        Assert.Equal(
            3,
            floorThreeMonsters
                .Select(static entry => (entry.Instance.X, entry.Instance.Y))
                .Distinct()
                .Count());

        var floorThreeSpawns = ParseMonsterSpawns(sent);
        Assert.Equal(3, floorThreeSpawns.Count);
        var floorThreeSpawnsByUid = floorThreeSpawns.ToDictionary(static spawn => spawn.Uid);
        foreach (var entry in floorThreeMonsters)
        {
            Assert.True(monsterStates.TryGetDynamicMonsterByUid(
                InstanceMapId,
                floorThreeDynamicId,
                entry.Instance.Uid,
                out var targetable,
                out var targetableFamily));
            Assert.Equal(entry.Instance.Uid, targetable.Uid);
            Assert.Equal(entry.Instance.X, targetable.X);
            Assert.Equal(entry.Instance.Y, targetable.Y);
            Assert.Equal(FloorThreeMonsterFamilyId, targetableFamily.Id);

            var spawn = floorThreeSpawnsByUid[entry.Instance.Uid];
            Assert.Equal((uint)FloorThreeMonsterFamilyId, spawn.MonsterId);
            Assert.Equal(entry.Instance.X, spawn.X);
            Assert.Equal(entry.Instance.Y, spawn.Y);
        }

        var currentSession = floorThreeSession;
        for (var killIndex = 0; killIndex < floorThreeMonsters.Length; killIndex++)
        {
            var target = floorThreeMonsters[killIndex].Instance;
            sent.Clear();

            await PacketHandlers.HandleAsync(
                CreateContext(connection, currentSession, worldData, sessions, monsterStates, log),
                ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(currentSession, target)).ToFrameBytes()));

            currentSession = GetSession(sessions, connection);
            var death = Assert.Single(
                ParseAttackPackets(sent),
                packet => packet.Query.AttackType == MsgAttackType.Death &&
                    packet.Query.OpponentUid == target.Uid);
            Assert.Equal(1, death.Query.KilledMonster);

            var remaining = monsterStates.GetVisibleDynamicMonsters(
                InstanceMapId,
                floorThreeDynamicId,
                DateTimeOffset.UtcNow);
            Assert.Equal(2 - killIndex, remaining.Count);
            Assert.DoesNotContain(remaining, entry => entry.Instance.Uid == target.Uid);

            if (killIndex < 2)
            {
                Assert.Equal("477", currentSession.Character.Attributes["SkyTowerCompletedChallengeId"]);
                Assert.Equal("10", currentSession.Character.Attributes["SkyTowerAvailableAttempts"]);
                Assert.Equal("478", currentSession.Character.Attributes["SkyTowerActiveChallengeId"]);
                Assert.Equal("3", currentSession.Character.Attributes["SkyTowerFloor"]);
                Assert.False(currentSession.Character.Attributes.ContainsKey("SkyTowerActiveCompletionId"));
                Assert.Empty(ParseInstanceInfoPackets(sent));

                var futureVisibleUids = monsterStates
                    .GetVisibleDynamicMonsters(
                        InstanceMapId,
                        floorThreeDynamicId,
                        DateTimeOffset.UtcNow.AddMinutes(5))
                    .Select(static entry => entry.Instance.Uid)
                    .Order()
                    .ToArray();
                var expectedAliveUids = floorThreeMonsters
                    .Skip(killIndex + 1)
                    .Select(static entry => entry.Instance.Uid)
                    .Order()
                    .ToArray();
                Assert.Equal(expectedAliveUids, futureVisibleUids);
                Assert.DoesNotContain(target.Uid, futureVisibleUids);
            }
        }

        Assert.Equal("478", currentSession.Character.Attributes["SkyTowerCompletedChallengeId"]);
        Assert.Equal("478", currentSession.Character.Attributes["SkyTowerActiveCompletionId"]);
        Assert.Equal("9", currentSession.Character.Attributes["SkyTowerAvailableAttempts"]);

        var completionPackets = ParseInstanceInfoPackets(sent);
        Assert.Equal(
            new uint[]
            {
                (uint)VisibleDx8D13InstanceInfoType.UserInstances,
                (uint)VisibleDx8D13InstanceInfoType.AvailableAttempts,
            },
            completionPackets.Select(static packet => packet.Type).ToArray());
        var progress = Assert.Single(completionPackets[0].UserInstances);
        Assert.Equal(FloorThreeChallengeId, progress.ChallengeId);
        Assert.Equal(1U, progress.CompletionFlag);
        var attempts = Assert.Single(completionPackets[1].Completions);
        Assert.Equal(LegacySkyTowerCatalog.RootChallengeId, attempts.ChallengeId);
        Assert.Equal(9U, attempts.AvailableAttempts);

        sent.Clear();
        await PacketHandlers.HandleAsync(
            CreateContext(connection, currentSession, worldData, sessions, monsterStates, log),
            ReadFrame(new LegacyInstancePacket(
                    LegacyInstanceAction.Info,
                    weather: FloorFourChallengeId,
                    userId: checked((uint)currentSession.Character.Uid))
                .ToFrameBytes()));

        var floorFourSession = GetSession(sessions, connection);
        var floorFourDynamicId = ReadDynamicId(floorFourSession);
        Assert.NotEqual(floorThreeDynamicId, floorFourDynamicId);
        Assert.Equal(InstanceMapId, floorFourSession.Character.MapId);
        Assert.Equal("479", floorFourSession.Character.Attributes["SkyTowerActiveChallengeId"]);
        Assert.Equal("4", floorFourSession.Character.Attributes["SkyTowerFloor"]);
        Assert.Equal("478", floorFourSession.Character.Attributes["SkyTowerCompletedChallengeId"]);
        Assert.Equal("9", floorFourSession.Character.Attributes["SkyTowerAvailableAttempts"]);
        Assert.False(floorFourSession.Character.Attributes.ContainsKey("SkyTowerActiveCompletionId"));
        Assert.Empty(monsterStates.GetVisibleDynamicMonsters(
            InstanceMapId,
            floorThreeDynamicId,
            DateTimeOffset.UtcNow));

        var floorFourMonsters = monsterStates
            .GetVisibleDynamicMonsters(InstanceMapId, floorFourDynamicId, DateTimeOffset.UtcNow)
            .OrderBy(static entry => entry.Instance.SpawnIndex)
            .ToArray();
        Assert.Equal(4, floorFourMonsters.Length);
        Assert.All(floorFourMonsters, entry =>
        {
            Assert.Equal((uint)FloorFourMonsterFamilyId, entry.Instance.MonsterId);
            Assert.Equal(FloorFourMonsterFamilyId, entry.MonsterFamily.Id);
            Assert.Equal(floorFourDynamicId, entry.Instance.DynamicId);
            Assert.InRange(entry.Instance.Uid, FirstDynamicMonsterUid, LastDynamicMonsterUid);
        });
        Assert.Equal(4, floorFourMonsters.Select(static entry => entry.Instance.Uid).Distinct().Count());
        Assert.Empty(
            floorFourMonsters.Select(static entry => entry.Instance.Uid).Intersect(
                floorThreeMonsters.Select(static entry => entry.Instance.Uid)));
        Assert.Equal(
            new (ushort X, ushort Y)[] { (61, 66), (62, 66), (60, 66), (61, 67) },
            floorFourMonsters.Select(static entry => (entry.Instance.X, entry.Instance.Y)).ToArray());

        var floorFourSpawns = ParseMonsterSpawns(sent);
        Assert.Equal(4, floorFourSpawns.Count);
        Assert.Equal(
            floorFourMonsters.Select(static entry => entry.Instance.Uid).Order().ToArray(),
            floorFourSpawns.Select(static spawn => spawn.Uid).Order().ToArray());
        Assert.All(floorFourSpawns, static spawn =>
            Assert.Equal((uint)FloorFourMonsterFamilyId, spawn.MonsterId));
        foreach (var entry in floorFourMonsters)
        {
            Assert.True(monsterStates.TryGetDynamicMonsterByUid(
                InstanceMapId,
                floorFourDynamicId,
                entry.Instance.Uid,
                out var targetable,
                out var targetableFamily));
            Assert.Equal(entry.Instance.Uid, targetable.Uid);
            Assert.Equal(FloorFourMonsterFamilyId, targetableFamily.Id);
        }
    }

    private static WorldPacketHandlerContext CreateContext(
        ConquerTcpConnection connection,
        WorldSession session,
        WorldDataCatalog worldData,
        WorldSessionStore sessions,
        MonsterRuntimeStateStore monsterStates,
        TextWriter log)
    {
        return new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1008,
                Face: 154,
                Hair: 27,
                Class: 7051,
                FirstClass: 0,
                SecondClass: 7050,
                Reborn: 2,
                Level: 140,
                Experience: 0,
                MapId: 1002,
                X: 425,
                Y: 404,
                PreviousMapId: 1000,
                PreviousMapX: 500,
                PreviousMapY: 650,
                Strength: 1_000,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 12,
                MinHitPoints: 61_258,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 7,
                SourceFile: "fixture://sky-tower-floor-three-progression",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SkyTowerCompletedChallengeId"] = "477",
                    ["SkyTowerAvailableAttempts"] = "10",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static Monster CreateMonsterFamily(int id)
    {
        return new Monster(
            id,
            $"SkyTower{id}",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "1",
                ["lookface"] = "171",
                ["level"] = "140",
                ["defense"] = "100",
                ["defence"] = "100",
                ["defense2"] = "10000",
                ["defence2"] = "10000",
                ["attack_range"] = "1",
                ["attack_user"] = "3",
            },
            []);
    }

    private static MsgAttackQuery CreatePhysicalQuery(
        WorldSession session,
        LegacyMonsterInstance monster)
    {
        return new MsgAttackQuery(
            Uid: checked((uint)session.Character.Uid),
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Physical,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
    }

    private static WorldSession GetSession(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static ulong ReadDynamicId(WorldSession session)
    {
        return ulong.Parse(session.Character.Attributes["DynamicID"], CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseAttackPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMonsterSpawnPacket> ParseMonsterSpawns(IEnumerable<byte[]> frames)
    {
        var spawns = new List<LegacyMonsterSpawnPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                spawns.Add(packet);
            }
        }

        return spawns;
    }

    private static IReadOnlyList<VisibleDx8D13InstanceInfoPacket> ParseInstanceInfoPackets(
        IEnumerable<byte[]> frames)
    {
        var packets = new List<VisibleDx8D13InstanceInfoPacket>();
        foreach (var bytes in frames)
        {
            if (VisibleDx8D13InstanceInfoPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static MapMetadata CreateMapMetadata(int id, long type)
    {
        return new MapMetadata(
            id,
            $"{id}.ini",
            "Anima",
            MapDoc: id,
            TypeRaw: type.ToString(CultureInfo.InvariantCulture),
            Type: type,
            Portal0X: null,
            Portal0Y: null,
            RebornMap: 1002,
            RebornPortal: 0,
            ResLev: 0,
            LinkMap: 0,
            LinkX: 0,
            LinkY: 0,
            ColorRaw: "0",
            Color: 0,
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
