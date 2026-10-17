using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerMonsterPhysicalCombatRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const uint ClubItemType = 480_000;
    private const uint TwoHandWeaponItemType = 500_109;
    private const int PhysicalDamageIncreaseItemType = 300_000;
    private const ushort ClubWeaponProcSpellId = 1_250;
    private const ushort DirectLearnedWeaponProcSpellId = 14_720;
    private const ushort WindstormSpellId = 11_140;
    private const ushort RightChopWeaponProcSpellId = 14_580;
    private const uint RightChopWeaponItemType = 671_439;
    private const uint RightChopWeaponTypes = 60_671_670;
    private const ushort PoisonWeaponEffectProcSpellId = 3_090;
    private const ushort MonsterHunterSpellId = 15_950;
    private const uint PoisonWeaponEffect = 200;
    private const int LegacyFocusedFlag = 0x15;
    private const int LegacyStigmaFlag = 0x09;
    private const uint SourceDefaultExpRateProf = 1_000;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public void PlayerMonsterPhysicalAttackHelperUsesSourceRollHalfNotMidpoint()
    {
        Assert.Equal(1_004U, InvokePlayerMonsterPhysicalAttackRoll(1_000, 1_010, deterministicRoll: 8));
        Assert.Equal(1_004U, InvokePlayerMonsterPhysicalAttackRoll(1_000, 1_010, deterministicRoll: 9));
        Assert.Equal(1_000U, InvokePlayerMonsterPhysicalAttackRoll(1_000, 1_000, deterministicRoll: 0));
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitUsesServerDamageWhenClientPacketDamageIsInflated()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(CreateEquippedClub());
        var statusFields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            maxHitpoints: checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            CombatGearSnapshot.Empty);
        var expectedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            statusFields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            deterministicRoll: 0);
        Assert.Contains(session.PlayerItems.EquipmentItems, item => item.Durability > 0);
        var runtimeStatusFields = InvokeRuntimeStatusFields(session, worldData);
        Assert.Equal(statusFields[LegacyPlayerStatusPacket.MinAttackIndex], runtimeStatusFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(statusFields[LegacyPlayerStatusPacket.MaxAttackIndex], runtimeStatusFields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.NotEqual(4_000U, expectedDamage);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Physical,
            Damage: 4_000,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
        Assert.Equal(
            expectedDamage,
            InvokePlayerMonsterPhysicalDamage(context, session, monster, monsterFamily, query));

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        Assert.NotEmpty(sent);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(sent[0]), out var response));
        Assert.Equal(monster.Uid, response.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Physical, response.Query.AttackType);
        Assert.Equal(checked((int)expectedDamage), response.Query.Damage);
        Assert.Equal(0, response.Query.KilledMonster);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - expectedDamage, damagedMonster.Hitpoints);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitDoesNotSendImmediateMonsterRetaliation()
    {
        var monsterFamily = CreateMonsterFamily(attributes: new Dictionary<string, string>
        {
            ["attack_range"] = "3",
            ["attack_min"] = "1000",
            ["attack_max"] = "1000",
        });
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(CreateEquippedClub());
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var physical = Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Equal(PlayerUid, physical.Query.Uid);
        Assert.DoesNotContain(
            sent,
            bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.Uid == monster.Uid &&
                packet.Query.OpponentUid == PlayerUid);
        Assert.DoesNotContain(ParseSpellAnimations(sent), animation => animation.AttackerUid == monster.Uid);
        var output = log.ToString();
        Assert.Contains("WorldServer handled monster combat", output);
        Assert.DoesNotContain("WorldServer sent legacy monster retaliation", output);
        Assert.DoesNotContain("WorldServer sent legacy magic monster retaliation", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitConsumesFocusedAfterApplyingOneAttackBoost()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var baselineSession = CreateSession(CreateEquippedClub());
        var session = CreateSession(
            CreateEquippedClub(),
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyFocusedFlag),
                ["IntensifyDamage"] = "200",
            });
        var baselineDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(CreateSendingConnection([]), TextWriter.Null, baselineSession, WorldData: worldData),
            baselineSession,
            monster,
            monsterFamily,
            CreatePhysicalQuery(monster));
        var expectedFocusedDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(CreateSendingConnection([]), TextWriter.Null, session, WorldData: worldData),
            session,
            monster,
            monsterFamily,
            CreatePhysicalQuery(monster));
        Assert.True(expectedFocusedDamage > baselineDamage);

        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var physical = Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Equal(checked((int)expectedFocusedDamage), physical.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasStatusFlag(updatedSession.Character.Attributes, LegacyFocusedFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("doFocus"));
        Assert.False(updatedSession.Character.Attributes.ContainsKey("DoFocus"));
        Assert.Contains("WorldServer consumed legacy Focused attack", log.ToString());
    }

    [Fact]
    public async Task HandlerArcherMonsterAttackBeyondLegacyRangeSuppressesNormalFallback()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 849, boundY: 584);
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(CreateEquippedClub());
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var distance = Math.Max(
            Math.Abs(session.Character.X - monster.X),
            Math.Abs(session.Character.Y - monster.Y));
        Assert.Equal(19, distance);
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Archer,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        Assert.Empty(ParseAttacks(sent, monster.Uid, MsgAttackType.Archer));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var unchangedMonster));
        Assert.Equal(monster.MaxLife, unchangedMonster.Hitpoints);
        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer monster target", output);
        Assert.Contains("monsterUid=" + monster.Uid.ToString(CultureInfo.InvariantCulture), output);
        Assert.Contains("distance=19 range=18 autoAttack=false", output);
        Assert.Contains("source=MsgAttackPacket.cs AttackID.Archer monster branch Base.GetDistance <= 18 gate", output);
        Assert.DoesNotContain("WorldServer handled monster combat", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitRejectsSourceBlockedFamily5385()
    {
        var monsterFamily = CreateMonsterFamily(id: 5_385);
        var spawn = CreateMonsterSpawn(monsterTypeId: 5_385);
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(CreateEquippedClub());
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var unchangedMonster));
        Assert.Equal(monster.MaxLife, unchangedMonster.Hitpoints);
        var output = log.ToString();
        Assert.Contains("CanAttackMonster.Verified rejected source-blocked monster family id 5385", output);
        Assert.DoesNotContain("WorldServer handled monster combat", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitRejectsSourceReviverSettingsEvenWithoutReviverName()
    {
        var monsterFamily = CreateMonsterFamily(
            id: 9_002,
            name: "TrainingShrine",
            attributes: new Dictionary<string, string>
            {
                ["settings"] = "98",
            });
        var spawn = CreateMonsterSpawn(monsterTypeId: 9_002, sourceMonsterName: "TrainingShrine");
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(CreateEquippedClub());
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var unchangedMonster));
        Assert.Equal(monster.MaxLife, unchangedMonster.Hitpoints);
        var output = log.ToString();
        Assert.Contains("CanAttackMonster.Verified rejected Reviver monster family settings=98", output);
        Assert.DoesNotContain("WorldServer handled monster combat", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitRequiresPkModeOnlyForGuardFamily()
    {
        var peaceModeAttributes = new Dictionary<string, string>
        {
            ["PkMode"] = "1",
        };
        var normalFamily = CreateMonsterFamily();
        var normalSpawn = CreateMonsterSpawn();
        var normalWorldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [normalFamily],
            monsterSpawns: [normalSpawn]);
        var normalMonster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            normalSpawn,
            normalFamily,
            normalWorldData.GetLegacyMonsterRuntimeUidBase(normalSpawn)));
        var normalSessions = new WorldSessionStore();
        var normalMonsterStates = new MonsterRuntimeStateStore();
        var normalSent = new List<byte[]>();
        using var normalLog = new StringWriter();
        var normalConnection = CreateSendingConnection(normalSent);
        var normalSession = CreateSession(CreateEquippedClub(), attributes: peaceModeAttributes);
        normalSessions.Set(normalConnection, normalSession);
        var normalContext = new WorldPacketHandlerContext(
            normalConnection,
            normalLog,
            normalSession,
            WorldData: normalWorldData,
            Sessions: normalSessions,
            MonsterStates: normalMonsterStates);

        await PacketHandlers.HandleAsync(
            normalContext,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(normalMonster)).ToFrameBytes()));

        var normalPhysical = Assert.Single(ParsePhysicalAttacks(normalSent, normalMonster.Uid));
        Assert.True(normalPhysical.Query.Damage > 0);
        Assert.True(normalMonsterStates.TryGetVisibleInstance(normalMonster, DateTimeOffset.UtcNow, out var damagedNormal));
        Assert.True(damagedNormal.Hitpoints < normalMonster.MaxLife);
        Assert.DoesNotContain("CanAttackMonster.Verified rejected Guard monster family", normalLog.ToString());

        var guardFamily = CreateMonsterFamily(
            id: 900,
            name: "Guard1",
            attributes: new Dictionary<string, string>
            {
                ["settings"] = "164",
            });
        var guardSpawn = CreateMonsterSpawn(monsterTypeId: 900, sourceMonsterName: "Guard1");
        var guardWorldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [guardFamily],
            monsterSpawns: [guardSpawn]);
        var guardMonster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            guardSpawn,
            guardFamily,
            guardWorldData.GetLegacyMonsterRuntimeUidBase(guardSpawn)));
        var guardSessions = new WorldSessionStore();
        var guardMonsterStates = new MonsterRuntimeStateStore();
        var guardSent = new List<byte[]>();
        using var guardLog = new StringWriter();
        var guardConnection = CreateSendingConnection(guardSent);
        var guardSession = CreateSession(CreateEquippedClub(), attributes: peaceModeAttributes);
        guardSessions.Set(guardConnection, guardSession);
        var guardContext = new WorldPacketHandlerContext(
            guardConnection,
            guardLog,
            guardSession,
            WorldData: guardWorldData,
            Sessions: guardSessions,
            MonsterStates: guardMonsterStates);

        await PacketHandlers.HandleAsync(
            guardContext,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(guardMonster)).ToFrameBytes()));

        Assert.Empty(ParsePhysicalAttacks(guardSent, guardMonster.Uid));
        Assert.True(guardMonsterStates.TryGetVisibleInstance(guardMonster, DateTimeOffset.UtcNow, out var unchangedGuard));
        Assert.Equal(guardMonster.MaxLife, unchangedGuard.Hitpoints);
        var guardOutput = guardLog.ToString();
        Assert.Contains("CanAttackMonster.Verified rejected Guard monster family because pkMode=1", guardOutput);
        Assert.Contains("familyId=900 familyName=Guard1 monsterName=Guard1 settings=164 guardSource=family.settings", guardOutput);
        Assert.DoesNotContain("WorldServer handled monster combat", guardOutput);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitRejectsOwnedLottusFloorMonster()
    {
        var monsterFamily = CreateMonsterFamily(
            id: 12_370,
            name: "AuroraLotus",
            attributes: new Dictionary<string, string>
            {
                ["settings"] = "2048",
            });
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: []);
        var monster = CreateDynamicMonster(monsterFamily, runtimeOwnerUid: PlayerUid);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        monsterStates.RegisterDynamicMonster(monster, monsterFamily);
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(CreateEquippedClub());
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var unchangedMonster));
        Assert.Equal(monster.MaxLife, unchangedMonster.Hitpoints);
        var output = log.ToString();
        Assert.Contains("CanAttackMonster.Verified rejected owned Lottus floor monster", output);
        Assert.DoesNotContain("WorldServer handled monster combat", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitAllowsUnownedLottusFloorMonster()
    {
        var monsterFamily = CreateMonsterFamily(
            id: 12_370,
            name: "AuroraLotus",
            attributes: new Dictionary<string, string>
            {
                ["settings"] = "2048",
            });
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: []);
        var monster = CreateDynamicMonster(monsterFamily, runtimeOwnerUid: PlayerUid + 1);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        monsterStates.RegisterDynamicMonster(monster, monsterFamily);
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(CreateEquippedClub());
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var physical = Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(physical.Query.Damage > 0);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < monster.MaxLife);
        var output = log.ToString();
        Assert.Contains("WorldServer handled monster combat", output);
        Assert.DoesNotContain("CanAttackMonster.Verified rejected owned Lottus floor monster", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitAppliesSourceDefaultExpRateProfBeforeWeaponProficiencyScaling()
    {
        var profsRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-LegacyMonsterWeaponProficiency-" + Guid.NewGuid().ToString("N"));
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var spawn = CreateMonsterSpawn();
            var worldData = CreateCatalog(
                items: [CreateClubItemType()],
                monsters: [monsterFamily],
                monsterSpawns: [spawn]);
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
            var startingProficiency = new PlayerProficiencySnapshotEntry(
                480,
                Level: 5,
                PreviousLevel: 0,
                Experience: 123,
                NeededExperience: LegacyProficiencyPacket.GetNeededExperience(5));
            var session = CreateSession(
                CreateEquippedClub(),
                playerProficiencies: new PlayerProficiencySnapshot([startingProficiency]));
            var expectedDamage = InvokePlayerMonsterPhysicalDamage(
                new WorldPacketHandlerContext(CreateSendingConnection([]), TextWriter.Null, session, WorldData: worldData),
                session,
                monster,
                monsterFamily,
                CreatePhysicalQuery(monster));
            var sourceScaledRawExperience = checked((uint)(expectedDamage * (ulong)SourceDefaultExpRateProf));
            var expectedProficiency = LegacyProficiencyPacket.ApplyExperience(
                startingProficiency,
                sourceScaledRawExperience).Proficiency;
            var unscaledProficiency = LegacyProficiencyPacket.ApplyExperience(
                startingProficiency,
                expectedDamage).Proficiency;
            Assert.NotEqual(unscaledProficiency, expectedProficiency);
            var sessions = new WorldSessionStore();
            var monsterStates = new MonsterRuntimeStateStore();
            var sent = new List<byte[]>();
            using var log = new StringWriter();
            var connection = CreateSendingConnection(sent);
            sessions.Set(connection, session);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                PlayerProficienciesRoot: profsRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

            var packet = Assert.Single(ParseProficiencyPackets(sent));
            Assert.Equal(LegacyProficiencyPacket.FromSnapshot(expectedProficiency), packet);
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal([expectedProficiency], updatedSession.PlayerProficiencies.Proficiencies);

            var persisted = new PlayerProficiencySnapshotLoader(profsRoot).LoadByCharacterUid(PlayerUid);
            Assert.Equal([expectedProficiency], persisted.Proficiencies);
            Assert.Contains("legacy monster weapon proficiency experience", log.ToString());
        }
        finally
        {
            if (Directory.Exists(profsRoot))
            {
                Directory.Delete(profsRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerPhysicalMonsterOverkillDisplaysRolledDamageAndStillSendsDeath()
    {
        var monsterFamily = CreateMonsterFamily(life: 100);
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(CreateEquippedClub());
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = CreatePhysicalQuery(monster);
        var expectedDamage = InvokePlayerMonsterPhysicalDamage(
            context,
            session,
            monster,
            monsterFamily,
            query);
        Assert.True(expectedDamage > monster.MaxLife);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        Assert.True(MsgAttackPacket.TryParse(ReadFrame(sent[0]), out var response));
        Assert.Equal(monster.Uid, response.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Physical, response.Query.AttackType);
        Assert.Equal(checked((int)expectedDamage), response.Query.Damage);
        Assert.Equal(0, response.Query.KilledMonster);

        Assert.True(MsgAttackPacket.TryParse(ReadFrame(sent[1]), out var death));
        Assert.Equal(PlayerUid, death.Query.Uid);
        Assert.Equal(monster.Uid, death.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Death, death.Query.AttackType);
        Assert.Equal(0, death.Query.Damage);
        Assert.Equal(1, death.Query.KilledMonster);
    }

    [Fact]
    public void NormalPlayerMonsterPhysicalDamageAppliesPhysicalDamageIncreaseAfterDefense2()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var baselineWorldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var boostedWorldData = CreateCatalog(
            items: [CreateClubItemType(), CreatePhysicalDamageIncreaseItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            baselineWorldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var baselineSession = CreateSession(CreateEquippedClub());
        var boostedSession = CreateSession(CreateEquippedClub(includePhysicalDamageIncreaseItem: true));
        var baselineFields = LegacyPlayerStatusCalculator.CalculateFields(
            baselineSession.Character,
            baselineSession.PlayerItems,
            baselineWorldData,
            maxHitpoints: checked((uint)baselineSession.Character.MinHitPoints),
            prestigeLevel: 0,
            CombatGearSnapshot.Empty);
        var boostedFields = LegacyPlayerStatusCalculator.CalculateFields(
            boostedSession.Character,
            boostedSession.PlayerItems,
            boostedWorldData,
            maxHitpoints: checked((uint)boostedSession.Character.MinHitPoints),
            prestigeLevel: 0,
            CombatGearSnapshot.Empty);
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Physical,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
        var baselineContext = new WorldPacketHandlerContext(
            CreateSendingConnection([]),
            TextWriter.Null,
            baselineSession,
            WorldData: baselineWorldData);
        var boostedContext = new WorldPacketHandlerContext(
            CreateSendingConnection([]),
            TextWriter.Null,
            boostedSession,
            WorldData: boostedWorldData);

        var baselineCalculatorDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            baselineFields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)baselineSession.Character.Level),
            monsterLevel: monster.Level,
            deterministicRoll: 0);
        var boostedCalculatorDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            boostedFields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)boostedSession.Character.Level),
            monsterLevel: monster.Level,
            deterministicRoll: 0);
        var baselineRuntimeDamage = InvokePlayerMonsterPhysicalDamage(
            baselineContext,
            baselineSession,
            monster,
            monsterFamily,
            query);
        var boostedRuntimeDamage = InvokePlayerMonsterPhysicalDamage(
            boostedContext,
            boostedSession,
            monster,
            monsterFamily,
            query);

        Assert.Equal(0U, baselineFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(9_000U, boostedFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(9_000U, boostedCalculatorDamage - baselineCalculatorDamage);
        Assert.Equal(9_000U, boostedRuntimeDamage - baselineRuntimeDamage);
    }

    [Fact]
    public void NormalPlayerMonsterPhysicalDamageAppliesSourceStigmaAjustAttack()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var baselineSession = CreateSession(CreateEquippedClub());
        var stigmaSession = CreateSession(
            CreateEquippedClub(),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyStigmaFlag),
            });
        var stigmaFields = LegacyPlayerStatusCalculator.CalculateFields(
            stigmaSession.Character,
            stigmaSession.PlayerItems,
            worldData,
            maxHitpoints: checked((uint)stigmaSession.Character.MinHitPoints),
            prestigeLevel: 0,
            CombatGearSnapshot.Empty);
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Physical,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
        var baselineContext = new WorldPacketHandlerContext(
            CreateSendingConnection([]),
            TextWriter.Null,
            baselineSession,
            WorldData: worldData);
        var stigmaContext = new WorldPacketHandlerContext(
            CreateSendingConnection([]),
            TextWriter.Null,
            stigmaSession,
            WorldData: worldData);

        var baselineRuntimeDamage = InvokePlayerMonsterPhysicalDamage(
            baselineContext,
            baselineSession,
            monster,
            monsterFamily,
            query);
        var stigmaRuntimeDamage = InvokePlayerMonsterPhysicalDamage(
            stigmaContext,
            stigmaSession,
            monster,
            monsterFamily,
            query);
        var expectedStigmaDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            stigmaFields[LegacyPlayerStatusPacket.MinAttackIndex],
            stigmaFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)stigmaSession.Character.Level),
            monsterLevel: monster.Level,
            finalPhysicalIncreasePercent: stigmaFields[LegacyPlayerStatusPacket.PhysicalPercentIndex],
            deterministicRoll: 0,
            hasStigmaOrAttackPotion: true);

        Assert.True(stigmaRuntimeDamage > baselineRuntimeDamage);
        Assert.Equal(expectedStigmaDamage, stigmaRuntimeDamage);
    }

    [Fact]
    public void NormalPlayerMonsterPhysicalDamageAppliesSourceRuneAjustAttackBonus()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var baselineSession = CreateSession(CreateEquippedClub());
        var runeSession = CreateSession(
            CreateEquippedClub(),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SharpnessLevel"] = "9",
            });
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Physical,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
        var baselineContext = new WorldPacketHandlerContext(
            CreateSendingConnection([]),
            TextWriter.Null,
            baselineSession,
            WorldData: worldData);
        var runeContext = new WorldPacketHandlerContext(
            CreateSendingConnection([]),
            TextWriter.Null,
            runeSession,
            WorldData: worldData);

        var baselineRuntimeDamage = InvokePlayerMonsterPhysicalDamage(
            baselineContext,
            baselineSession,
            monster,
            monsterFamily,
            query);
        var runeRuntimeDamage = InvokePlayerMonsterPhysicalDamage(
            runeContext,
            runeSession,
            monster,
            monsterFamily,
            query);

        Assert.Equal(2_180U, baselineRuntimeDamage);
        Assert.Equal(2_564U, runeRuntimeDamage);
    }

    [Fact]
    public void NormalPlayerMonsterPhysicalDamageAppliesBossKillerAndMonsterHunterBonus()
    {
        var monsterFamily = CreateMonsterFamily(attributes: new Dictionary<string, string>
        {
            ["Boss"] = "1",
        });
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(MonsterHunterSpellId, damage: 30_300)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        Assert.True(monster.Boss);
        var boostedSession = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(MonsterHunterSpellId)]),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BossKillerLevel"] = "1",
            });
        var fields = InvokeRuntimeStatusFields(boostedSession, worldData);
        var expectedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            fields[LegacyPlayerStatusPacket.MinAttackIndex],
            fields[LegacyPlayerStatusPacket.MaxAttackIndex],
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)boostedSession.Character.Level),
            monsterLevel: monster.Level,
            deterministicRoll: 0,
            monsterDamageBonusPercent: 520);

        var runtimeDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(
                CreateSendingConnection([]),
                TextWriter.Null,
                boostedSession,
                WorldData: worldData),
            boostedSession,
            monster,
            monsterFamily,
            CreatePhysicalQuery(monster));

        // Source: BossKiller +1 contributes 220, learned MonsterHunter GDamage 30300 contributes 300.
        Assert.Equal(expectedDamage, runtimeDamage);
    }

    [Fact]
    public void NormalPlayerMonsterPhysicalDamageRequiresMatchingRuneForRightWeaponKind()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType(), CreateClubItemType(TwoHandWeaponItemType)],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var oneHandWideSwipeSession = CreateSession(
            CreateEquippedClub(),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["WideSwipeLevel"] = "9",
            });
        var twoHandSharpnessSession = CreateSession(
            CreateEquippedClub(itemType: TwoHandWeaponItemType),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SharpnessLevel"] = "9",
            });
        var twoHandWideSwipeSession = CreateSession(
            CreateEquippedClub(itemType: TwoHandWeaponItemType),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["WideSwipeLevel"] = "9",
            });
        var query = CreatePhysicalQuery(monster);

        var oneHandWideSwipeDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(
                CreateSendingConnection([]),
                TextWriter.Null,
                oneHandWideSwipeSession,
                WorldData: worldData),
            oneHandWideSwipeSession,
            monster,
            monsterFamily,
            query);
        var twoHandSharpnessDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(
                CreateSendingConnection([]),
                TextWriter.Null,
                twoHandSharpnessSession,
                WorldData: worldData),
            twoHandSharpnessSession,
            monster,
            monsterFamily,
            query);
        var twoHandWideSwipeDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(
                CreateSendingConnection([]),
                TextWriter.Null,
                twoHandWideSwipeSession,
                WorldData: worldData),
            twoHandWideSwipeSession,
            monster,
            monsterFamily,
            query);

        Assert.Equal(2_180U, oneHandWideSwipeDamage);
        Assert.Equal(2_180U, twoHandSharpnessDamage);
        Assert.Equal(2_564U, twoHandWideSwipeDamage);
    }

    [Fact]
    public void PlayerMonsterMagicDamageStillAppliesMagicDamageIncrease()
    {
        var session = CreateSession(CreateEquippedClub());
        var spell = new LegacyMagicRuntimeSpell(
            Id: 1_000,
            Sort: LegacyMagicSort.Attack,
            Name: "MagicDamageIncreaseParity",
            Level: 0,
            UseMana: 0,
            Damage: 0,
            Experience: 0,
            MaxTargets: 1,
            Range: 10,
            NeedLevel: 1,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);
        var baselineFields = new uint[LegacyPlayerStatusPacket.FieldCount];
        var boostedFields = new uint[LegacyPlayerStatusPacket.FieldCount];
        baselineFields[LegacyPlayerStatusPacket.MagicAttackIndex] = 1_000;
        boostedFields[LegacyPlayerStatusPacket.MagicAttackIndex] = 1_000;
        boostedFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex] = 9_000;
        var monsterFamily = CreateMonsterFamily();

        var baselineDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            monsterFamily,
            spell,
            baselineFields);
        var boostedDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamage(
            session,
            worldData: null,
            monsterFamily,
            spell,
            boostedFields);

        Assert.Equal(9_000U, boostedDamage - baselineDamage);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitUsesServerDamageWhenLiveClientPacketOmitsDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(CreateEquippedClub());
        var statusFields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            maxHitpoints: checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0,
            CombatGearSnapshot.Empty);
        var expectedDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            statusFields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: checked((int)session.Character.Level),
            monsterLevel: monster.Level,
            deterministicRoll: 0);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Physical,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        Assert.NotEmpty(sent);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(sent[0]), out var response));
        Assert.Equal(monster.Uid, response.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Physical, response.Query.AttackType);
        Assert.Equal(checked((int)expectedDamage), response.Query.Damage);
        Assert.DoesNotContain("packet damage is zero", log.ToString(), StringComparison.OrdinalIgnoreCase);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - expectedDamage, damagedMonster.Hitpoints);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitWeaponProcMissesRateAndUsesNormalPhysicalDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(ClubWeaponProcSpellId, weaponType: 480, rate: 10)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(ClubWeaponProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponSpellRateRoll"] = "99",
            });
        var expectedDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(CreateSendingConnection([]), TextWriter.Null, session, WorldData: worldData),
            session,
            monster,
            monsterFamily,
            CreatePhysicalQuery(monster));
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        var physical = Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Equal(checked((int)expectedDamage), physical.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitWeaponProcSetsRandomSpellAndDispatchesMagic()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(ClubWeaponProcSpellId, weaponType: 480, rate: 10)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(ClubWeaponProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponSpellRateRoll"] = "0",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(ClubWeaponProcSpellId, animation.SpellId);
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains("WorldServer handled legacy weapon proc monster attack", log.ToString());
    }

    [Fact]
    public async Task HandlerNewerClientPhysicalMonsterHitSkipsLegacyWeaponProcAndUsesNormalPhysicalDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(ClubWeaponProcSpellId, weaponType: 480, rate: 10)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(ClubWeaponProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponSpellRateRoll"] = "0",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains("WorldServer skipped legacy weapon proc monster attack", log.ToString());
        Assert.DoesNotContain("WorldServer handled legacy weapon proc monster attack", log.ToString());
    }

    [Fact]
    public async Task HandlerNewerClientPhysicalMonsterHitWithWeaponProcFenceDisabledRestoresLegacyWeaponProc()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(ClubWeaponProcSpellId, weaponType: 480, rate: 10)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(ClubWeaponProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponSpellRateRoll"] = "0",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient with
            {
                DisableLegacyWeaponProcMonsterAttacks = false,
            });

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(ClubWeaponProcSpellId, animation.SpellId);
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains("WorldServer handled legacy weapon proc monster attack", log.ToString());
        Assert.DoesNotContain("WorldServer skipped legacy weapon proc monster attack", log.ToString());
    }

    [Fact]
    public async Task HandlerNewerClientPhysicalMonsterHitDispatchesRightChopPerimeterAnimation()
    {
        var monsterFamily = CreateMonsterFamily(life: 100_000);
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType(RightChopWeaponItemType)],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic:
            [
                CreateMagicRow(
                    RightChopWeaponProcSpellId,
                    sort: 78,
                    damage: 100,
                    maxTargets: 3,
                    range: 0,
                    weaponType: RightChopWeaponTypes,
                    rate: 100),
            ]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(itemType: RightChopWeaponItemType),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(RightChopWeaponProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponSpellRateRoll.14580"] = "0",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient with
            {
                DisableLegacyWeaponProcMonsterAttacks = false,
            });

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(RightChopWeaponProcSpellId, animation.SpellId);
        Assert.Equal(monster.X, animation.X);
        Assert.Equal(monster.Y, animation.Y);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(target.Hit);
        Assert.True(target.Damage > 0);
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Contains("spellId=14580 sort=78", log.ToString());
        Assert.Contains("WorldServer handled legacy weapon proc monster attack", log.ToString());
    }

    [Fact]
    public async Task HandlerNewerClientPhysicalMonsterHitWithWeaponProcFenceDisabledRunsPostPhysicalWindstorm()
    {
        var monsterFamily = CreateMonsterFamily(life: 100_000);
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic:
            [
                CreateMagicRow(
                    WindstormSpellId,
                    sort: 55,
                    damage: 0,
                    maxTargets: 30,
                    range: 0),
            ]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(WindstormSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellDirectRateRoll.11140.Primary"] = "99",
                ["LegacyWeaponSpellDirectRateRoll.11140.DeadlySight"] = "99",
                ["LegacyWeaponSpellOuterRateRoll"] = "99",
                ["LegacyWeaponSpellDirectRateRoll.11140.PostPhysicalPrimary"] = "0",
                ["LegacyWeaponSpellDirectRateRoll.11140.PostPhysicalDeadlySight"] = "99",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient with
            {
                DisableLegacyWeaponProcMonsterAttacks = false,
            });

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(WindstormSpellId, animation.SpellId);
        Assert.Equal(3, animation.Targets.Count);
        Assert.All(animation.Targets, target => Assert.Equal(monster.Uid, target.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        var output = log.ToString();
        Assert.Contains("WorldServer handled monster combat", output);
        Assert.Contains("source=MsgAttackPacket.Physical monster post-hit PostPhysicalPrimary", output);
        Assert.DoesNotContain("WorldServer skipped legacy weapon proc monster attack", output);
    }

    [Fact]
    public async Task HandlerNewerClientPhysicalMonsterHitKeepsPostPhysicalWindstormBehindWeaponProcFence()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic:
            [
                CreateMagicRow(
                    WindstormSpellId,
                    sort: 55,
                    damage: 0,
                    maxTargets: 30,
                    range: 0),
            ]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(WindstormSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellDirectRateRoll.11140.Primary"] = "99",
                ["LegacyWeaponSpellOuterRateRoll"] = "99",
                ["LegacyWeaponSpellDirectRateRoll.11140.PostPhysicalPrimary"] = "0",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            ClientCompatibility: LegacyClientCompatibilityOptions.NewerClient);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Contains("WorldServer skipped legacy weapon proc monster attack", log.ToString());
        Assert.DoesNotContain("source=MsgAttackPacket.Physical monster post-hit", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitDirectLearnedWeaponProcSetsRandomSpellAndDispatchesMagic()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(DirectLearnedWeaponProcSpellId)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(DirectLearnedWeaponProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellDirectRateRoll.14720"] = "0",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(DirectLearnedWeaponProcSpellId, animation.SpellId);
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains("WorldServer handled legacy weapon proc monster attack", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitDirectLearnedWeaponProcMissesRateAndUsesNormalPhysicalDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(DirectLearnedWeaponProcSpellId)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(DirectLearnedWeaponProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellDirectRateRoll.14720"] = "99",
                ["LegacyWeaponSpellOuterRateRoll"] = "99",
            });
        var expectedDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(CreateSendingConnection([]), TextWriter.Null, session, WorldData: worldData),
            session,
            monster,
            monsterFamily,
            CreatePhysicalQuery(monster));
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        var physical = Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Equal(checked((int)expectedDamage), physical.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitLearnedRightWeaponEffectProcSetsRandomSpellAndDispatchesMagic()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(PoisonWeaponEffectProcSpellId)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(weaponEffect: PoisonWeaponEffect),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(PoisonWeaponEffectProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponEffectRateRoll.3090"] = "0",
            });
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PoisonWeaponEffectProcSpellId, animation.SpellId);
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(PoisonWeaponEffectProcSpellId, Assert.Single(updatedSession.PlayerSpells.Spells).Id);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains("WorldServer handled legacy weapon proc monster attack", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitUnlearnedRightWeaponEffectIsConsumedWithoutMagicOrPhysicalDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(PoisonWeaponEffectProcSpellId)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(weaponEffect: PoisonWeaponEffect),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponEffectRateRoll.3090"] = "0",
            }) with
        {
            LegacyRandomSpell = PoisonWeaponEffectProcSpellId,
        };
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.DoesNotContain(
            sent,
            bytes => ReadFrame(bytes).Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Empty(updatedSession.PlayerSpells.Spells);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        var output = log.ToString();
        Assert.Contains(
            "outcome=consumed-no-dispatchable-spell learned=false metadataResolved=true",
            output);
        Assert.Contains(
            "source=GetWeaponSpell.cs:358-377 -> MsgAttackPacket.cs:910-911 ProcescMagic ClientSpells gate",
            output);
        Assert.DoesNotContain("WorldServer handled legacy weapon proc monster attack", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitLearnedRightWeaponEffectMissingMagicMetadataIsConsumedWithoutMagicOrPhysicalDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: []);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(weaponEffect: PoisonWeaponEffect),
            playerSpells: new PlayerSpellSnapshot([CreatePlayerSpell(PoisonWeaponEffectProcSpellId)]),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponEffectRateRoll.3090"] = "0",
            }) with
        {
            LegacyRandomSpell = PoisonWeaponEffectProcSpellId,
        };
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.DoesNotContain(
            sent,
            bytes => ReadFrame(bytes).Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(PoisonWeaponEffectProcSpellId, Assert.Single(updatedSession.PlayerSpells.Spells).Id);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        var output = log.ToString();
        Assert.Contains(
            "outcome=consumed-no-dispatchable-spell learned=true metadataResolved=false",
            output);
        Assert.Contains(
            "source=GetWeaponSpell.cs:358-377 -> MsgAttackPacket.cs:910-911 ProcescMagic ClientSpells gate",
            output);
        Assert.DoesNotContain("WorldServer handled legacy weapon proc monster attack", output);
    }

    [Fact]
    public async Task HandlerPhysicalMonsterHitRightWeaponEffectProcMissesRateAndUsesNormalPhysicalDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateCatalog(
            items: [CreateClubItemType()],
            monsters: [monsterFamily],
            monsterSpawns: [spawn],
            magic: [CreateMagicRow(PoisonWeaponEffectProcSpellId)]);
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
        var session = CreateSession(
            CreateEquippedClub(weaponEffect: PoisonWeaponEffect),
            attributes: new Dictionary<string, string>
            {
                ["LegacyWeaponSpellOuterRateRoll"] = "0",
                ["LegacyWeaponEffectRateRoll.3090"] = "99",
            });
        var expectedDamage = InvokePlayerMonsterPhysicalDamage(
            new WorldPacketHandlerContext(CreateSendingConnection([]), TextWriter.Null, session, WorldData: worldData),
            session,
            monster,
            monsterFamily,
            CreatePhysicalQuery(monster));
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreatePhysicalQuery(monster)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        var physical = Assert.Single(ParsePhysicalAttacks(sent, monster.Uid));
        Assert.Equal(checked((int)expectedDamage), physical.Query.Damage);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Empty(updatedSession.PlayerSpells.Spells);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    private static uint InvokePlayerMonsterPhysicalAttackRoll(
        uint minAttack,
        uint maxAttack,
        uint deterministicRoll)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "SelectLegacyPlayerMonsterPhysicalAttack",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [minAttack, maxAttack, (uint?)deterministicRoll]));
    }

    private static uint InvokePlayerMonsterPhysicalDamage(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        MsgAttackQuery query)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyPlayerMonsterDamage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint>(method.Invoke(null, [context, session, monster, monsterFamily, query]));
    }

    private static uint[] InvokeRuntimeStatusFields(WorldSession session, WorldDataCatalog worldData)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "CalculateLegacyRuntimeStatusFields",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<uint[]>(method.Invoke(null, [session, worldData]));
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[10];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words) + "/";
    }

    private static bool HasStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        var raw = attributes.TryGetValue("StatusFlags", out var statusFlags)
            ? statusFlags
            : attributes.TryGetValue("BitVector", out var bitVector)
                ? bitVector
                : string.Empty;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordIndex = flag / 32;
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        return (word & (1UL << (flag % 32))) != 0;
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot? playerSpells = null,
        PlayerProficiencySnapshot? playerProficiencies = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var characterAttributes = attributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
        return new WorldSession(
            AccountId: 1,
            Username: "physical-combat",
            new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "PhysicalCombat",
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: 142,
                FirstClass: 142,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: 1_015,
                X: 830,
                Y: 584,
                PreviousMapId: 1_015,
                PreviousMapX: 830,
                PreviousMapY: 584,
                Strength: 1_000,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 5_000,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://physical-combat",
                Attributes: characterAttributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            playerItems,
            playerSpells ?? PlayerSpellSnapshot.Empty,
            playerProficiencies ?? PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
    }

    private static MsgAttackQuery CreatePhysicalQuery(LegacyMonsterInstance monster)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
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

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(ushort spellId, ushort level = 0)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            level,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static PlayerItemSnapshot CreateEquippedClub(
        bool includePhysicalDamageIncreaseItem = false,
        uint weaponEffect = 0,
        uint itemType = ClubItemType)
    {
        var equipment = new List<PlayerItemSnapshotItem>
        {
            new()
            {
                ItemId = itemType,
                Durability = 100,
                MaximumDurability = 100,
                Position = 4,
                Effect = weaponEffect,
            },
        };
        if (includePhysicalDamageIncreaseItem)
        {
            equipment.Add(new PlayerItemSnapshotItem
            {
                ItemId = PhysicalDamageIncreaseItemType,
                Durability = 100,
                MaximumDurability = 100,
                Position = 10,
            });
        }

        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipment,
            RuneItems: []);
    }

    private static ItemType CreateClubItemType(uint itemType = ClubItemType)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemType.ToString(CultureInfo.InvariantCulture);
        fields[1] = "SourceClueClub";
        fields[13] = "1000";
        fields[14] = "1000";
        return new ItemType(checked((int)itemType), "SourceClueClub", fields);
    }

    private static ItemType CreatePhysicalDamageIncreaseItemType()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = PhysicalDamageIncreaseItemType.ToString(CultureInfo.InvariantCulture);
        fields[1] = "SourceCluePhysicalDamageIncrease";
        fields[13] = "9000";
        return new ItemType(
            PhysicalDamageIncreaseItemType,
            "SourceCluePhysicalDamageIncrease",
            fields);
    }

    private static MagicType CreateMagicRow(
        ushort spellId,
        int sort = 1,
        byte level = 0,
        ushort useMana = 0,
        uint damage = 120,
        uint experience = 0,
        byte maxTargets = 1,
        byte range = 10,
        ushort needLevel = 1,
        uint weaponType = 0,
        byte useStamina = 0,
        ushort coolDown = 0,
        int coldTime = 0,
        uint rate = 100)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = $"Spell{spellId}";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = useMana.ToString(CultureInfo.InvariantCulture);
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[12] = rate.ToString(CultureInfo.InvariantCulture);
        fields[13] = "0";
        fields[14] = maxTargets.ToString(CultureInfo.InvariantCulture);
        fields[15] = range.ToString(CultureInfo.InvariantCulture);
        fields[18] = experience.ToString(CultureInfo.InvariantCulture);
        fields[20] = needLevel.ToString(CultureInfo.InvariantCulture);
        fields[22] = weaponType.ToString(CultureInfo.InvariantCulture);
        fields[29] = useStamina.ToString(CultureInfo.InvariantCulture);
        fields[33] = coolDown.ToString(CultureInfo.InvariantCulture);
        fields[47] = coldTime.ToString(CultureInfo.InvariantCulture);
        return new MagicType(spellId, spellId, $"Spell{spellId}", fields);
    }

    private static Monster CreateMonsterFamily(
        uint life = 5_000,
        int id = 9_001,
        string name = "TrainingMonster",
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var familyAttributes = attributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
        familyAttributes.TryAdd("life", life.ToString(CultureInfo.InvariantCulture));
        familyAttributes.TryAdd("lookface", "100");
        familyAttributes.TryAdd("level", "120");
        familyAttributes.TryAdd("defense", "100");
        familyAttributes.TryAdd("defence", "100");
        familyAttributes.TryAdd("defense2", "10000");
        familyAttributes.TryAdd("defence2", "10000");
        familyAttributes.TryAdd("attack_range", "0");

        return new Monster(
            id,
            name,
            familyAttributes,
            []);
    }

    private static LegacyMonsterInstance CreateDynamicMonster(Monster monsterFamily, uint runtimeOwnerUid = 0)
    {
        return new LegacyMonsterInstance(
            Uid: 900_001,
            MapId: 1_015,
            X: 832,
            Y: 584,
            Mesh: 100,
            Level: 120,
            Hitpoints: 5_000,
            MaxLife: 5_000,
            MonsterId: checked((uint)monsterFamily.Id),
            monsterFamily.Name,
            Boss: false,
            GeneratorId: 0,
            SpawnIndex: 0,
            RestSeconds: 10,
            DynamicId: 0,
            IsFloor: true,
            runtimeOwnerUid);
    }

    private static MonsterSpawn CreateMonsterSpawn(
        ushort boundX = 832,
        ushort boundY = 584,
        int monsterTypeId = 9_001,
        string sourceMonsterName = "TrainingMonster")
    {
        return new MonsterSpawn(
            GeneratorId: 1,
            MapId: 1_015,
            MonsterTypeId: monsterTypeId,
            BoundX: boundX,
            BoundY: boundY,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: 1_015,
            SourceMonsterName: sourceMonsterName,
            SourceRelativePath: Path.Combine("1015", sourceMonsterName, "1.ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = "1",
                ["mapid"] = "1015",
                ["npctype"] = monsterTypeId.ToString(CultureInfo.InvariantCulture),
            });
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static IReadOnlyList<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgAttackPacket> ParsePhysicalAttacks(
        IEnumerable<byte[]> frames,
        uint opponentUid)
    {
        return ParseAttacks(frames, opponentUid, MsgAttackType.Physical);
    }

    private static IReadOnlyList<MsgAttackPacket> ParseAttacks(
        IEnumerable<byte[]> frames,
        uint opponentUid,
        MsgAttackType attackType)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == attackType &&
                packet.Query.OpponentUid == opponentUid)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyProficiencyPacket> ParseProficiencyPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyProficiencyPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyProficiencyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<Monster> monsters,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<MagicType>? magic = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-19T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://player-monster-physical-combat",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magic ?? [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: monsters,
            monsterSpawns: monsterSpawns,
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
