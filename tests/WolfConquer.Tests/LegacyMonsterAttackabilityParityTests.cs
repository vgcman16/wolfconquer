using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterAttackabilityParityTests
{
    private const uint PlayerUid = 1_293_600;
    private const int MapId = 1_015;
    private const uint DynamicId = 77;
    private const uint ObservedD13PeaceMode = 3;
    private const uint FreePkMode = 0;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData(400_000U, false, "TrainingMonster", "TrainingMonster")]
    [InlineData(900_000U, true, "FlameDevil", "FlameDevil")]
    [InlineData(900_001U, true, "TrainingMonster", "ReviverProjection")]
    [InlineData(900_002U, true, "ChaosGuard", "ChaosGuard")]
    public async Task OrdinaryClientRangeMonstersAreAttackableOnObservedD13PeacePath(
        uint uid,
        bool dynamic,
        string familyName,
        string instanceName)
    {
        using var fixture = CreateFixture(
            CreateMonsterFamily(9_001, familyName),
            uid,
            dynamic,
            ObservedD13PeaceMode,
            instanceName: instanceName);

        await AttackAsync(fixture);

        var attack = Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.True(attack.Query.Damage > 0);
        Assert.True(fixture.MonsterStates.TryGetVisibleInstance(
            fixture.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.Equal(
            fixture.Monster.Hitpoints - checked((uint)attack.Query.Damage),
            damagedMonster.Hitpoints);
        Assert.DoesNotContain("rejected Guard monster family", fixture.Log.ToString());
        Assert.DoesNotContain("rejected Reviver monster family", fixture.Log.ToString());
    }

    [Fact]
    public async Task RegisteredHighUidMonsterIsRejectedAsD13PkTargetIdentity()
    {
        using var fixture = CreateFixture(
            CreateMonsterFamily(3_546, "FlameDevil"),
            uid: 110_428_001,
            dynamic: true,
            pkMode: ObservedD13PeaceMode);

        await AttackAsync(fixture);

        Assert.Empty(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.True(fixture.MonsterStates.TryGetVisibleInstance(
            fixture.Monster,
            DateTimeOffset.UtcNow,
            out var unchangedMonster));
        Assert.Equal(fixture.Monster.MaxLife, unchangedMonster.Hitpoints);
        Assert.Contains(
            "D13 client monster identity rejected uid=110428001 expectedRange=400000..999999",
            fixture.Log.ToString());
    }

    [Fact]
    public async Task RegisteredHighUidMonsterRemainsAttackableOutsideVisibleD13Profile()
    {
        using var fixture = CreateFixture(
            CreateMonsterFamily(3_546, "FlameDevil"),
            uid: 1_000_001,
            dynamic: true,
            pkMode: ObservedD13PeaceMode,
            protocolProfile: ConquerProtocolProfile.RootD11_7899);

        await AttackAsync(fixture);

        Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.DoesNotContain("D13 client monster identity rejected", fixture.Log.ToString());
    }

    [Theory]
    [InlineData(ProtectedMonsterKind.Guard, "rejected Guard monster family because pkMode=3")]
    [InlineData(ProtectedMonsterKind.Reviver, "rejected Reviver monster family")]
    [InlineData(ProtectedMonsterKind.OwnedLottus, "rejected owned Lottus floor monster")]
    [InlineData(ProtectedMonsterKind.Family5385, "rejected source-blocked monster family id 5385")]
    public async Task ProtectedSourceFamiliesRemainBlockedOnObservedD13PeacePath(
        ProtectedMonsterKind kind,
        string expectedDiagnostic)
    {
        var (family, ownerUid, isFloor) = kind switch
        {
            ProtectedMonsterKind.Guard => (CreateMonsterFamily(900, "Guard1"), 0U, false),
            ProtectedMonsterKind.Reviver => (CreateMonsterFamily(901, "Reviver"), 0U, false),
            ProtectedMonsterKind.OwnedLottus =>
                (CreateMonsterFamily(12_370, "AuroraLotus", settings: 2_048), PlayerUid, true),
            ProtectedMonsterKind.Family5385 => (CreateMonsterFamily(5_385, "ProtectedMonster"), 0U, false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        using var fixture = CreateFixture(
            family,
            uid: 900_100,
            dynamic: true,
            pkMode: ObservedD13PeaceMode,
            runtimeOwnerUid: ownerUid,
            isFloor: isFloor);

        await AttackAsync(fixture);

        Assert.Empty(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.True(fixture.MonsterStates.TryGetVisibleInstance(
            fixture.Monster,
            DateTimeOffset.UtcNow,
            out var unchangedMonster));
        Assert.Equal(fixture.Monster.MaxLife, unchangedMonster.Hitpoints);
        Assert.Contains(expectedDiagnostic, fixture.Log.ToString());
    }

    [Fact]
    public async Task UnownedLottusRemainsAttackableOnObservedD13PeacePath()
    {
        using var fixture = CreateFixture(
            CreateMonsterFamily(12_370, "AuroraLotus", settings: 2_048),
            uid: 900_101,
            dynamic: true,
            pkMode: ObservedD13PeaceMode,
            runtimeOwnerUid: PlayerUid + 1,
            isFloor: true);

        await AttackAsync(fixture);

        Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.DoesNotContain("rejected owned Lottus floor monster", fixture.Log.ToString());
    }

    [Fact]
    public async Task GuardBecomesAttackableOnlyInFreePkModeAndAppliesFlashingName()
    {
        using var fixture = CreateFixture(
            CreateMonsterFamily(900, "Guard1"),
            uid: 900_102,
            dynamic: true,
            pkMode: FreePkMode);

        await AttackAsync(fixture);

        Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        var output = fixture.Log.ToString();
        Assert.Contains("applied legacy guard attack flashing-name", output);
        Assert.DoesNotContain("rejected Guard monster family", output);
    }

    private static async Task AttackAsync(AttackFixture fixture)
    {
        await PacketHandlers.HandleAsync(
            fixture.Context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: fixture.Monster.Uid,
                X: fixture.Monster.X,
                Y: fixture.Monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));
    }

    private static AttackFixture CreateFixture(
        Monster family,
        uint uid,
        bool dynamic,
        uint pkMode,
        string? instanceName = null,
        uint runtimeOwnerUid = 0,
        bool isFloor = false,
        ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.VisibleDx8D13)
    {
        var spawn = dynamic ? null : CreateMonsterSpawn(family);
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            monsters: [family],
            monsterSpawns: spawn is null ? [] : [spawn]);
        var monster = spawn is null
            ? CreateDynamicMonster(family, uid, instanceName, runtimeOwnerUid, isFloor)
            : Assert.Single(LegacyMonsterInstance.FromGenerator(
                spawn,
                family,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        Assert.Equal(uid, monster.Uid);

        var monsterStates = new MonsterRuntimeStateStore();
        if (dynamic)
        {
            monsterStates.RegisterDynamicMonster(monster, family);
        }

        var session = CreateSession(pkMode, dynamic ? DynamicId : 0);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ProtocolProfile: protocolProfile);
        return new AttackFixture(connection, sessions, monsterStates, sent, log, context, monster);
    }

    private static Monster CreateMonsterFamily(int id, string name, uint? settings = null)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["life"] = "5000",
            ["lookface"] = "100",
            ["level"] = "120",
            ["defense"] = "100",
            ["defence"] = "100",
            ["defense2"] = "10000",
            ["defence2"] = "10000",
            ["attack_range"] = "1",
        };
        if (settings is not null)
        {
            attributes["settings"] = settings.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new Monster(id, name, attributes, []);
    }

    private static MonsterSpawn CreateMonsterSpawn(Monster family)
    {
        return new MonsterSpawn(
            GeneratorId: 1,
            MapId: MapId,
            MonsterTypeId: family.Id,
            BoundX: 833,
            BoundY: 584,
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
            SourceMonsterName: family.Name,
            SourceRelativePath: Path.Combine(MapId.ToString(CultureInfo.InvariantCulture), family.Name, "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = "1",
                ["mapid"] = MapId.ToString(CultureInfo.InvariantCulture),
                ["npctype"] = family.Id.ToString(CultureInfo.InvariantCulture),
            });
    }

    private static LegacyMonsterInstance CreateDynamicMonster(
        Monster family,
        uint uid,
        string? instanceName,
        uint runtimeOwnerUid,
        bool isFloor)
    {
        return new LegacyMonsterInstance(
            uid,
            MapId,
            X: 833,
            Y: 584,
            Mesh: 100,
            Level: 120,
            Hitpoints: 5_000,
            MaxLife: 5_000,
            MonsterId: checked((uint)family.Id),
            Name: instanceName ?? family.Name,
            Boss: false,
            GeneratorId: checked((int)uid),
            SpawnIndex: 0,
            RestSeconds: 10,
            DynamicId: DynamicId,
            IsFloor: isFloor,
            RuntimeOwnerUid: runtimeOwnerUid);
    }

    private static WorldSession CreateSession(uint pkMode, uint dynamicId)
    {
        return new WorldSession(
            AccountId: PlayerUid,
            Username: "attackability",
            Character: new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "Attackability",
                Body: 1003,
                Face: 67,
                Hair: 310,
                Class: 1_000,
                FirstClass: 0,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: MapId,
                X: 832,
                Y: 584,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 380,
                Strength: 1_000,
                Agility: 60,
                Vitality: 100,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 5_000,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://legacy-monster-attackability",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PkMode"] = pkMode.ToString(CultureInfo.InvariantCulture),
                    ["DynamicID"] = dynamicId.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static IReadOnlyList<MsgAttackPacket> ParsePhysicalAttacks(
        IEnumerable<byte[]> frames,
        uint opponentUid)
    {
        var attacks = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical &&
                packet.Query.OpponentUid == opponentUid)
            {
                attacks.Add(packet);
            }
        }

        return attacks;
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

    public enum ProtectedMonsterKind
    {
        Guard,
        Reviver,
        OwnedLottus,
        Family5385,
    }

    private sealed record AttackFixture(
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions,
        MonsterRuntimeStateStore MonsterStates,
        List<byte[]> Sent,
        StringWriter Log,
        WorldPacketHandlerContext Context,
        LegacyMonsterInstance Monster) : IDisposable
    {
        public void Dispose()
        {
            Log.Dispose();
        }
    }
}
