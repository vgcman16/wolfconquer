using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldServerFrameHandlerMonsterAggroParityTests
{
    private const uint PlayerUid = 1_293_600;
    private const int MapId = 1_011;

    [Fact]
    public async Task RuntimeTickWithoutPriorPlayerHitAttacksStandardMonsterAndSkipsSettingsGuard()
    {
        var standardFamily = CreateMonsterFamily(
            id: 910,
            name: "Pheasant",
            attackUser: 3,
            settings: 0x35);
        var settingsGuardFamily = CreateMonsterFamily(
            id: 911,
            name: "Sentinel",
            attackUser: 15,
            settings: 0xA4);
        var standardSpawn = CreateSpawn(generatorId: 910, monsterTypeId: 910, monsterName: "Pheasant", x: 201, y: 200);
        var settingsGuardSpawn = CreateSpawn(generatorId: 911, monsterTypeId: 911, monsterName: "Sentinel", x: 200, y: 201);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [standardFamily, settingsGuardFamily],
            monsterSpawns: [standardSpawn, settingsGuardSpawn]);
        var standardMonster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            standardSpawn,
            standardFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(standardSpawn)));
        var settingsGuard = Assert.Single(LegacyMonsterInstance.FromGenerator(
            settingsGuardSpawn,
            settingsGuardFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(settingsGuardSpawn)));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            LegacyRuntimeUpdateLoopOptions.Disabled,
            log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        GetSessions(handler).Set(connection, CreateSession());

        await InvokeMonsterAttackTickAsync(
            handler,
            new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero));

        var attacks = new List<MsgAttackPacket>();
        foreach (var bytes in sent)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack))
            {
                attacks.Add(attack);
            }
        }

        Assert.Contains(
            attacks,
            attack => attack.Query.Uid == standardMonster.Uid &&
                attack.Query.OpponentUid == PlayerUid);
        Assert.DoesNotContain(
            attacks,
            attack => attack.Query.Uid == settingsGuard.Uid &&
                attack.Query.OpponentUid == PlayerUid);
    }

    [Fact]
    public async Task RuntimeTickWithoutPriorPlayerHitAttacksSkyTowerDynamicMonsterInPlayerInstance()
    {
        const uint dynamicId = 77;
        var family = CreateMonsterFamily(
            id: 6_074,
            name: "FlameDevil",
            attackUser: 3,
            settings: 0x35,
            attackRange: 1);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: [family]);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            LegacyRuntimeUpdateLoopOptions.Disabled,
            log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        GetSessions(handler).Set(connection, CreateSession(dynamicId));
        var monster = CreateDynamicMonster(family, dynamicId);
        var otherInstanceMonster = CreateDynamicMonster(family, dynamicId + 1, uid: 900_001);
        var monsterStates = GetMonsterStates(handler);
        monsterStates.RegisterDynamicMonster(monster, family);
        monsterStates.RegisterDynamicMonster(otherInstanceMonster, family);

        await InvokeMonsterAttackTickAsync(
            handler,
            new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero));

        var attacks = new List<MsgAttackPacket>();
        foreach (var bytes in sent)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack))
            {
                attacks.Add(attack);
            }
        }

        Assert.Contains(
            attacks,
            attack => attack.Query.Uid == monster.Uid &&
                attack.Query.OpponentUid == PlayerUid);
        Assert.DoesNotContain(
            attacks,
            attack => attack.Query.Uid == otherInstanceMonster.Uid);
    }

    [Fact]
    public async Task DynamicLifecycleTicksBroadcastRemovalBeforeRespawn()
    {
        const uint dynamicId = 77;
        var family = CreateMonsterFamily(
            id: 912,
            name: "DynamicPheasant",
            attackUser: 3,
            settings: 0x35);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: [family]);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            LegacyRuntimeUpdateLoopOptions.Disabled,
            log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        GetSessions(handler).Set(connection, CreateSession(dynamicId));
        var monsterStates = GetMonsterStates(handler);
        var monster = CreateDynamicMonster(family, dynamicId);
        monsterStates.RegisterDynamicMonster(monster, family);

        var deathAt = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        Assert.True(monsterStates.ApplyDamage(monster, monster.MaxLife, deathAt).Killed);
        var lifecycleAt = deathAt.AddMinutes(1);
        Assert.False(monsterStates.TryGetVisibleInstance(monster, lifecycleAt, out _));

        await InvokeMonsterRuntimeTickAsync(handler, "SendDueMonsterDeathRemovalsAsync", lifecycleAt);
        await InvokeMonsterRuntimeTickAsync(handler, "SendDueMonsterRespawnsAsync", lifecycleAt);

        var removeIndex = FindFrameIndex(
            sent,
            frame => MsgActionPacket.TryParse(frame, out var action) &&
                action.ObjId == monster.Uid &&
                action.Type == MsgActionType.RemoveEntity);
        var spawnIndex = FindFrameIndex(
            sent,
            frame => LegacyMonsterSpawnPacket.TryParse(frame, out var spawn) &&
                spawn.Uid == monster.Uid &&
                spawn.MaxLife == monster.MaxLife);
        var effectIndex = FindFrameIndex(
            sent,
            frame => MsgActionPacket.TryParse(frame, out var action) &&
                action.ObjId == monster.Uid &&
                action.Type == MsgActionType.MonsterRespawnEffect);

        Assert.InRange(removeIndex, 0, spawnIndex - 1);
        Assert.True(effectIndex > spawnIndex);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, lifecycleAt, out var respawned));
        Assert.Equal(monster.MaxLife, respawned.Hitpoints);
        Assert.Contains("WorldServer sent monster death remove monsterCount=1 viewerCount=1", log.ToString());
        Assert.Contains("WorldServer sent monster respawn monsterCount=1 viewerCount=1", log.ToString());
    }

    private static Monster CreateMonsterFamily(
        int id,
        string name,
        uint attackUser,
        uint settings,
        uint attackRange = 3)
    {
        return new Monster(
            id,
            name,
            new Dictionary<string, string>
            {
                ["life"] = "100",
                ["lookface"] = id.ToString(CultureInfo.InvariantCulture),
                ["level"] = "1",
                ["attack_range"] = attackRange.ToString(CultureInfo.InvariantCulture),
                ["view_range"] = "6",
                ["attack_speed"] = "1000",
                ["move_speed"] = "500",
                ["attack_min"] = "5",
                ["attack_max"] = "6",
                ["attack_user"] = attackUser.ToString(CultureInfo.InvariantCulture),
                ["settings"] = settings.ToString(CultureInfo.InvariantCulture),
            },
            []);
    }

    private static MonsterSpawn CreateSpawn(
        int generatorId,
        int monsterTypeId,
        string monsterName,
        int x,
        int y)
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId,
            MonsterTypeId: monsterTypeId,
            BoundX: x,
            BoundY: y,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: MapId,
            SourceMonsterName: monsterName,
            SourceRelativePath: Path.Combine(
                MapId.ToString(CultureInfo.InvariantCulture),
                monsterName,
                "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = generatorId.ToString(CultureInfo.InvariantCulture),
                ["mapid"] = MapId.ToString(CultureInfo.InvariantCulture),
                ["npctype"] = monsterTypeId.ToString(CultureInfo.InvariantCulture),
            });
    }

    private static LegacyMonsterInstance CreateDynamicMonster(
        Monster family,
        uint dynamicId,
        uint uid = 900_000)
    {
        return new LegacyMonsterInstance(
            Uid: uid,
            MapId,
            X: 201,
            Y: 200,
            Mesh: checked((uint)family.Id),
            Level: 1,
            Hitpoints: 100,
            MaxLife: 100,
            MonsterId: checked((uint)family.Id),
            Name: family.Name,
            Boss: false,
            GeneratorId: family.Id,
            SpawnIndex: 0,
            RestSeconds: 2,
            DynamicId: dynamicId);
    }

    private static WorldSession CreateSession(uint dynamicId = 0)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (dynamicId != 0)
        {
            attributes["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture);
        }

        return new WorldSession(
            AccountId: PlayerUid,
            Username: "user02",
            Character: new CharacterSnapshot(
                Uid: PlayerUid,
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
                MapId,
                X: 200,
                Y: 200,
                PreviousMapId: MapId,
                PreviousMapX: 200,
                PreviousMapY: 200,
                Strength: 0,
                Agility: 36,
                Vitality: 447,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1_000_000,
                MinMana: 5_000,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "1293600.ini",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static WorldSessionStore GetSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static MonsterRuntimeStateStore GetMonsterStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_monsterStates",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<MonsterRuntimeStateStore>(field.GetValue(handler));
    }

    private static async ValueTask InvokeMonsterAttackTickAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        await InvokeMonsterRuntimeTickAsync(handler, "SendDueMonsterAttacksAsync", now);
    }

    private static async ValueTask InvokeMonsterRuntimeTickAsync(
        WorldServerFrameHandler handler,
        string methodName,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var pending = Assert.IsType<ValueTask>(method.Invoke(handler, [now, CancellationToken.None]));
        await pending;
    }

    private static int FindFrameIndex(
        IReadOnlyList<byte[]> frames,
        Func<PacketFrame, bool> predicate)
    {
        for (var index = 0; index < frames.Count; index++)
        {
            if (predicate(ReadFrame(frames[index])))
            {
                return index;
            }
        }

        return -1;
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
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }
}
