using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyInstanceMonsterCombatRuntimeTests
{
    private const int InstanceMapId = 10_429;
    private const int InstanceSourceMapId = 10_428;
    private const int MonsterFamilyId = 3_546;
    private const uint FirstDynamicMonsterUid = 900_000;
    private const uint LastDynamicMonsterUid = 999_999;
    private const ushort HolySanctionPassiveSpellId = 21_670;
    private const uint PirateCombatGearItemId = 701;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task InfoRegistersMonsterForCurrentLogicalMapAndDynamicInstance()
    {
        var fixture = await EnterInstanceAsync();
        using var log = fixture.Log;

        Assert.Equal(InstanceMapId, fixture.Monster.MapId);
        Assert.Equal(fixture.DynamicId, fixture.Monster.DynamicId);
        Assert.Equal((uint)MonsterFamilyId, fixture.Monster.MonsterId);
        Assert.InRange(fixture.Monster.Uid, FirstDynamicMonsterUid, LastDynamicMonsterUid);
        Assert.True(fixture.MonsterStates.TryGetDynamicMonsterByUid(
            InstanceMapId,
            fixture.DynamicId,
            fixture.Monster.Uid,
            out var registered,
            out var monsterFamily));
        Assert.Equal(fixture.Monster.Uid, registered.Uid);
        Assert.Equal(fixture.DynamicId, registered.DynamicId);
        Assert.Equal(MonsterFamilyId, monsterFamily.Id);
    }

    [Fact]
    public async Task PhysicalAttackInPeaceModeResolvesAndDamagesRegisteredInstanceMonster()
    {
        var fixture = await EnterInstanceAsync();
        using var log = fixture.Log;
        Assert.InRange(fixture.Monster.Uid, FirstDynamicMonsterUid, LastDynamicMonsterUid);
        var peacefulAttributes = new Dictionary<string, string>(
            fixture.Session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["PkMode"] = "3",
        };
        var peacefulSession = fixture.Session with
        {
            Character = fixture.Session.Character with { Attributes = peacefulAttributes },
        };
        fixture.Sessions.Set(fixture.Connection, peacefulSession);
        fixture.Sent.Clear();

        await PacketHandlers.HandleAsync(
            fixture.CreateContext(peacefulSession),
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(peacefulSession, fixture.Monster)).ToFrameBytes()));

        var response = Assert.Single(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.True(response.Query.Damage > 0);
        Assert.True(fixture.MonsterStates.TryGetVisibleInstance(
            fixture.Monster,
            DateTimeOffset.UtcNow,
            out var damagedMonster));
        Assert.Equal(
            fixture.Monster.Hitpoints - checked((uint)response.Query.Damage),
            damagedMonster.Hitpoints);
        Assert.DoesNotContain("monster instance was not found", log.ToString());
    }

    [Fact]
    public async Task PhysicalAttackDoesNotResolveMonsterFromDifferentDynamicInstance()
    {
        var fixture = await EnterInstanceAsync();
        using var log = fixture.Log;
        var mismatchedAttributes = new Dictionary<string, string>(
            fixture.Session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = (fixture.DynamicId + 1).ToString(CultureInfo.InvariantCulture),
        };
        var mismatchedSession = fixture.Session with
        {
            Character = fixture.Session.Character with { Attributes = mismatchedAttributes },
        };
        fixture.Sessions.Set(fixture.Connection, mismatchedSession);
        fixture.Sent.Clear();

        await PacketHandlers.HandleAsync(
            fixture.CreateContext(mismatchedSession),
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(mismatchedSession, fixture.Monster)).ToFrameBytes()));

        Assert.Empty(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.True(fixture.MonsterStates.TryGetVisibleInstance(
            fixture.Monster,
            DateTimeOffset.UtcNow,
            out var untouchedMonster));
        Assert.Equal(fixture.Monster.MaxLife, untouchedMonster.Hitpoints);
        Assert.Contains(
            $"monsterUid={fixture.Monster.Uid}; monster instance was not found on map={InstanceMapId}",
            log.ToString());
    }

    [Fact]
    public async Task DuplicateInfoReusesRegisteredMonsterWithoutResettingRuntimeState()
    {
        var fixture = await EnterInstanceAsync();
        using var log = fixture.Log;
        Assert.InRange(fixture.Monster.Uid, FirstDynamicMonsterUid, LastDynamicMonsterUid);
        var damage = fixture.MonsterStates.ApplyDamage(
            fixture.Monster,
            requestedDamage: 12_345,
            DateTimeOffset.UtcNow);
        Assert.Equal(fixture.Monster.MaxLife - 12_345U, damage.RemainingHitpoints);
        fixture.Sent.Clear();

        await PacketHandlers.HandleAsync(
            fixture.CreateContext(fixture.Session),
            ReadFrame(new LegacyInstancePacket(LegacyInstanceAction.Info).ToFrameBytes()));

        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var replayedSession));
        Assert.NotNull(replayedSession);
        Assert.Equal(
            fixture.DynamicId.ToString(CultureInfo.InvariantCulture),
            replayedSession!.Character.Attributes["DynamicID"]);
        var registered = Assert.Single(fixture.MonsterStates.GetVisibleDynamicMonsters(
            InstanceMapId,
            fixture.DynamicId,
            DateTimeOffset.UtcNow));
        Assert.InRange(registered.Instance.Uid, FirstDynamicMonsterUid, LastDynamicMonsterUid);
        Assert.Equal(fixture.Monster.Uid, registered.Instance.Uid);
        Assert.Equal(damage.RemainingHitpoints, registered.Instance.Hitpoints);
        var replayedSpawn = Assert.Single(ParseMonsterSpawns(fixture.Sent));
        Assert.InRange(replayedSpawn.Uid, FirstDynamicMonsterUid, LastDynamicMonsterUid);
        Assert.Equal(fixture.Monster.Uid, replayedSpawn.Uid);
        Assert.Equal(damage.RemainingHitpoints, replayedSpawn.Hitpoints);
    }

    [Fact]
    public async Task D13FloorOneKillPushesProgressAndRemainingAttemptsOnce()
    {
        var fixture = await EnterInstanceAsync(
            instanceId: LegacySkyTowerCatalog.RootChallengeId,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        using var log = fixture.Log;
        Assert.Equal(6_072U, fixture.Monster.MonsterId);
        Assert.Equal(
            LegacySkyTowerCatalog.RootChallengeId.ToString(CultureInfo.InvariantCulture),
            fixture.Session.Character.Attributes["SkyTowerActiveChallengeId"]);
        fixture.MonsterStates.ApplyDamage(
            fixture.Monster,
            fixture.Monster.MaxLife - 1,
            DateTimeOffset.UtcNow);
        fixture.Sent.Clear();

        await PacketHandlers.HandleAsync(
            fixture.CreateContext(fixture.Session),
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(fixture.Session, fixture.Monster)).ToFrameBytes()));

        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var completedSession));
        Assert.NotNull(completedSession);
        Assert.Equal(
            "476",
            completedSession!.Character.Attributes["SkyTowerCompletedChallengeId"]);
        Assert.Equal(
            "476",
            completedSession.Character.Attributes["SkyTowerActiveCompletionId"]);
        Assert.Equal("9", completedSession.Character.Attributes["SkyTowerAvailableAttempts"]);
        var instanceInfoPackets = ParseInstanceInfoPackets(fixture.Sent);
        var progressPacket = Assert.Single(
            instanceInfoPackets,
            static packet => packet.Type == (uint)VisibleDx8D13InstanceInfoType.UserInstances);
        var progress = Assert.Single(progressPacket.UserInstances);
        Assert.Equal(476U, progress.ChallengeId);
        Assert.Equal(1U, progress.CompletionFlag);
        var attemptsPacket = Assert.Single(
            instanceInfoPackets,
            static packet => packet.Type == (uint)VisibleDx8D13InstanceInfoType.AvailableAttempts);
        var attempts = Assert.Single(attemptsPacket.Completions);
        Assert.Equal(476U, attempts.ChallengeId);
        Assert.Equal(9U, attempts.AvailableAttempts);
        Assert.Empty(fixture.MonsterStates.GetVisibleDynamicMonsters(
            InstanceMapId,
            fixture.DynamicId,
            DateTimeOffset.UtcNow.AddDays(1)));

        fixture.Sent.Clear();
        await PacketHandlers.HandleAsync(
            fixture.CreateContext(completedSession),
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(completedSession, fixture.Monster)).ToFrameBytes()));

        Assert.Empty(ParseInstanceInfoPackets(fixture.Sent));
        Assert.Contains("completed Sky Tower challenge", log.ToString());
    }

    [Fact]
    public async Task D13FloorOnePhysicalClickReplacedByHolySanctionStillCompletesChallenge()
    {
        var fixture = await EnterInstanceAsync(
            instanceId: LegacySkyTowerCatalog.RootChallengeId,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        using var log = fixture.Log;
        Assert.Equal(FirstDynamicMonsterUid, fixture.Monster.Uid);
        Assert.Equal(6_072U, fixture.Monster.MonsterId);
        RegisterFixtureMagicRow(fixture.WorldData, CreateHolySanctionMagicRow());
        var combatGearAttributes = new Dictionary<string, string>(
            fixture.Session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyCombatGearAttackSpellIndex"] = "0",
            ["LegacyCombatGearAttackRateRoll.21670"] = "0",
        };
        var combatGearSession = fixture.Session with
        {
            Character = fixture.Session.Character with { Attributes = combatGearAttributes },
            PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(HolySanctionPassiveSpellId)]),
            CombatGear = CreateEquippedPirateCombatGear(),
        };
        fixture.Sessions.Set(fixture.Connection, combatGearSession);
        var setupDamage = fixture.MonsterStates.ApplyDamage(
            fixture.Monster,
            fixture.Monster.MaxLife - 1,
            DateTimeOffset.UtcNow);
        Assert.Equal(1U, setupDamage.RemainingHitpoints);
        fixture.Sent.Clear();

        await PacketHandlers.HandleAsync(
            fixture.CreateContext(combatGearSession) with
            {
                ClientCompatibility = LegacyClientCompatibilityOptions.NewerClient,
            },
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(combatGearSession, fixture.Monster)).ToFrameBytes()));

        var replacement = Assert.Single(
            ParseSpellAnimations(fixture.Sent),
            animation => animation.SpellId == HolySanctionPassiveSpellId);
        var lethalTarget = Assert.Single(
            replacement.Targets,
            target => target.Uid == FirstDynamicMonsterUid);
        Assert.Equal(1U, lethalTarget.Damage);
        Assert.Empty(ParsePhysicalAttacks(fixture.Sent, fixture.Monster.Uid));
        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var completedSession));
        Assert.NotNull(completedSession);
        Assert.Equal("476", completedSession!.Character.Attributes["SkyTowerCompletedChallengeId"]);
        Assert.Equal("476", completedSession.Character.Attributes["SkyTowerActiveCompletionId"]);
        Assert.Equal("9", completedSession.Character.Attributes["SkyTowerAvailableAttempts"]);

        var instanceInfoPackets = ParseInstanceInfoPackets(fixture.Sent);
        Assert.Equal(
            new uint[]
            {
                (uint)VisibleDx8D13InstanceInfoType.UserInstances,
                (uint)VisibleDx8D13InstanceInfoType.AvailableAttempts,
            },
            instanceInfoPackets.Select(static packet => packet.Type).ToArray());
        var progress = Assert.Single(instanceInfoPackets[0].UserInstances);
        Assert.Equal(476U, progress.ChallengeId);
        Assert.Equal(1U, progress.CompletionFlag);
        var attempts = Assert.Single(instanceInfoPackets[1].Completions);
        Assert.Equal(476U, attempts.ChallengeId);
        Assert.Equal(9U, attempts.AvailableAttempts);
        Assert.Empty(fixture.MonsterStates.GetVisibleDynamicMonsters(
            InstanceMapId,
            fixture.DynamicId,
            DateTimeOffset.UtcNow.AddDays(1)));

        var output = log.ToString();
        Assert.Contains("replaced legacy monster physical attack with CombatGear spell", output);
        Assert.Contains("spellId=21670", output);
        Assert.Contains("completed Sky Tower challenge", output);
    }

    [Fact]
    public async Task D13NextFloorCreatesItsExactTwoMonsterObjective()
    {
        var fixture = await EnterInstanceAsync(
            instanceId: LegacySkyTowerCatalog.RootChallengeId,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        using var log = fixture.Log;
        fixture.MonsterStates.ApplyDamage(
            fixture.Monster,
            fixture.Monster.MaxLife - 1,
            DateTimeOffset.UtcNow);
        fixture.Sent.Clear();
        await PacketHandlers.HandleAsync(
            fixture.CreateContext(fixture.Session),
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(fixture.Session, fixture.Monster)).ToFrameBytes()));
        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var completedSession));
        Assert.NotNull(completedSession);
        fixture.Sent.Clear();

        await PacketHandlers.HandleAsync(
            fixture.CreateContext(completedSession!),
            ReadFrame(new LegacyInstancePacket(
                    LegacyInstanceAction.Info,
                    weather: 477,
                    userId: checked((uint)completedSession!.Character.Uid))
                .ToFrameBytes()));

        Assert.True(fixture.Sessions.TryGet(fixture.Connection, out var floorTwoSession));
        Assert.NotNull(floorTwoSession);
        var floorTwoDynamicId = ulong.Parse(
            floorTwoSession!.Character.Attributes["DynamicID"],
            CultureInfo.InvariantCulture);
        Assert.NotEqual(fixture.DynamicId, floorTwoDynamicId);
        Assert.Equal("477", floorTwoSession.Character.Attributes["SkyTowerActiveChallengeId"]);
        Assert.Equal("2", floorTwoSession.Character.Attributes["SkyTowerFloor"]);
        var floorTwoMonsters = fixture.MonsterStates.GetVisibleDynamicMonsters(
            InstanceMapId,
            floorTwoDynamicId,
            DateTimeOffset.UtcNow);
        Assert.Equal(2, floorTwoMonsters.Count);
        Assert.All(floorTwoMonsters, static entry => Assert.Equal(6_073, entry.MonsterFamily.Id));
        Assert.Equal(2, floorTwoMonsters.Select(static entry => entry.Instance.Uid).Distinct().Count());
        Assert.Equal(2, ParseMonsterSpawns(fixture.Sent).Count);
    }

    [Fact]
    public async Task D13AttemptsQueryRestoresPersistedTowerProgressAndAttempts()
    {
        var fixture = await EnterInstanceAsync(
            instanceId: LegacySkyTowerCatalog.RootChallengeId,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        using var log = fixture.Log;
        var attributes = new Dictionary<string, string>(
            fixture.Session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["SkyTowerCompletedChallengeId"] = "477",
            ["SkyTowerAvailableAttempts"] = "7",
        };
        var progressedSession = fixture.Session with
        {
            Character = fixture.Session.Character with { Attributes = attributes },
        };
        fixture.Sessions.Set(fixture.Connection, progressedSession);
        fixture.Sent.Clear();
        var query = new VisibleDx8D13InstanceInfoPacket(
            (uint)VisibleDx8D13InstanceInfoType.AvailableAttempts,
            [new VisibleDx8D13CompleteInstanceInfo(476, 0, HasAvailableAttempts: false)]);

        await PacketHandlers.HandleAsync(
            fixture.CreateContext(progressedSession),
            ReadFrame(query.ToFrameBytes()));

        var responses = ParseInstanceInfoPackets(fixture.Sent);
        var progressResponse = Assert.Single(
            responses,
            static packet => packet.Type == (uint)VisibleDx8D13InstanceInfoType.UserInstances);
        var progress = Assert.Single(progressResponse.UserInstances);
        Assert.Equal(477U, progress.ChallengeId);
        Assert.Equal(1U, progress.CompletionFlag);
        var attemptsResponse = Assert.Single(
            responses,
            static packet => packet.Type == (uint)VisibleDx8D13InstanceInfoType.AvailableAttempts);
        var attempts = Assert.Single(attemptsResponse.Completions);
        Assert.Equal(476U, attempts.ChallengeId);
        Assert.Equal(7U, attempts.AvailableAttempts);
    }

    private static async Task<InstanceFixture> EnterInstanceAsync(
        uint instanceId = 0,
        ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.RootD11_7899)
    {
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var worldData = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(
            maps: [CreateMapMetadata(InstanceSourceMapId, type: 18_685_966)],
            monsters: [CreateMonsterFamily()]);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                ProtocolProfile: protocolProfile),
            ReadFrame(new LegacyInstancePacket(
                    LegacyInstanceAction.Info,
                    weather: instanceId,
                    userId: checked((uint)session.Character.Uid))
                .ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var dynamicId = ulong.Parse(
            updatedSession!.Character.Attributes["DynamicID"],
            CultureInfo.InvariantCulture);
        var registered = Assert.Single(monsterStates.GetVisibleDynamicMonsters(
            InstanceMapId,
            dynamicId,
            DateTimeOffset.UtcNow));
        return new InstanceFixture(
            connection,
            sessions,
            monsterStates,
            worldData,
            sent,
            log,
            updatedSession,
            dynamicId,
            registered.Instance,
            protocolProfile);
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            MonsterFamilyId,
            "FlameDevil",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["life"] = "40000000",
                ["lookface"] = "171",
                ["level"] = "140",
                ["defense"] = "100",
                ["defence"] = "100",
                ["defense2"] = "10000",
                ["defence2"] = "10000",
                ["attack_range"] = "1",
            },
            []);
    }

    private static void RegisterFixtureMagicRow(WorldDataCatalog worldData, MagicType magicRow)
    {
        var magicById = Assert.IsAssignableFrom<IDictionary<int, MagicType>>(worldData.MagicById);
        var magicByTypeId = Assert.IsAssignableFrom<IDictionary<int, IReadOnlyList<MagicType>>>(
            worldData.MagicByTypeId);
        magicById.Add(magicRow.Id, magicRow);
        magicByTypeId.Add(magicRow.TypeId, [magicRow]);
    }

    private static MagicType CreateHolySanctionMagicRow()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = HolySanctionPassiveSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = HolySanctionPassiveSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = ((int)LegacyMagicSort.Bomb).ToString(CultureInfo.InvariantCulture);
        fields[3] = "HolySanction-Passive";
        fields[4] = "1";
        fields[10] = "30550";
        fields[12] = "30";
        fields[14] = "4";
        fields[22] = "612670";
        fields[24] = "16388";
        fields[30] = "1";
        fields[34] = "1";
        fields[35] = "30";
        fields[36] = "30200";
        return new MagicType(
            HolySanctionPassiveSpellId,
            HolySanctionPassiveSpellId,
            "HolySanction-Passive",
            fields);
    }

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static CombatGearSnapshot CreateEquippedPirateCombatGear()
    {
        return new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    PirateCombatGearItemId,
                    Level: 4,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
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
                SourceFile: "fixture://instance-monster-combat",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
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

    private static IReadOnlyList<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var animations = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                animations.Add(packet);
            }
        }

        return animations;
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

    private sealed record InstanceFixture(
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions,
        MonsterRuntimeStateStore MonsterStates,
        WorldDataCatalog WorldData,
        List<byte[]> Sent,
        StringWriter Log,
        WorldSession Session,
        ulong DynamicId,
        LegacyMonsterInstance Monster,
        ConquerProtocolProfile ProtocolProfile)
    {
        public WorldPacketHandlerContext CreateContext(WorldSession session)
        {
            return new WorldPacketHandlerContext(
                Connection,
                Log,
                session,
                WorldData: WorldData,
                Sessions: Sessions,
                MonsterStates: MonsterStates,
                ProtocolProfile: ProtocolProfile);
        }
    }
}
