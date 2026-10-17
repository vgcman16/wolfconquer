using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort TripleAttackPhysicalSpellsSpellId = 10_490;
    private const ushort WrathOfTheEmperorSpellId = 12_570;
    private const ushort AlternateWindstormPhysicalSpellsSpellId = 13_290;
    private const ushort AlternateTripleAttackPhysicalSpellsSpellId = 13_610;
    private const int LegacyPhysicalSpellsShurikenVortexFlag = 0x2E;
    private const int LegacyPhysicalSpellsManiacDanceFlag = 0xB5;

    [Fact]
    public async Task HandlerPhysicalSpellsTripleAttackAgainstVisibleMonsterUsesSourceSort55Route()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateTripleAttackPhysicalSpellsRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [TripleAttackPhysicalSpellsSpellId],
            legacyRandomSpell: TripleAttackPhysicalSpellsSpellId);
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
            ReadFrame(new MsgAttackPacket(
                CreateMagicQuery(monster, legacyEncodedQuery: false, TripleAttackPhysicalSpellsSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(TripleAttackPhysicalSpellsSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellEffect);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(monster.Uid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        var totalDamage = spell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - (uint)totalDamage, damagedMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("84", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy PhysicalSpells monster magic", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("line magic", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsTripleAttackAgainstVisibleFloorMonsterUsesSourceFloorDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateTripleAttackPhysicalSpellsRow());
        var monsterStates = new MonsterRuntimeStateStore();
        var floorMonster = monsterStates.RegisterDynamicMonster(
            new LegacyMonsterInstance(
                Uid: monsterStates.NextLegacyDynamicMonsterUid(),
                MapId: 1_015,
                X: 832,
                Y: 584,
                Mesh: 100,
                Level: 100,
                Hitpoints: 25,
                MaxLife: 25,
                MonsterId: checked((uint)monsterFamily.Id),
                Name: monsterFamily.Name,
                Boss: false,
                GeneratorId: 0,
                SpawnIndex: 0,
                IsFloor: true),
            monsterFamily);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [TripleAttackPhysicalSpellsSpellId],
            legacyRandomSpell: TripleAttackPhysicalSpellsSpellId);
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
            ReadFrame(new MsgAttackPacket(
                CreateMagicQuery(floorMonster, legacyEncodedQuery: false, TripleAttackPhysicalSpellsSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(TripleAttackPhysicalSpellsSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellEffect);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(floorMonster.Uid, target.Uid);
            Assert.Equal(2U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(floorMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(19U, damagedMonster.Hitpoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy PhysicalSpells monster magic", output);
        Assert.Contains("totalDamage=6", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsTripleAttackAgainstVisiblePlayerAppliesSourcePlayerMultiplierBeforeCallerDivisor()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateTripleAttackPhysicalSpellsRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [TripleAttackPhysicalSpellsSpellId],
            legacyRandomSpell: TripleAttackPhysicalSpellsSpellId);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(
            casterSession.Character,
            casterSession.PlayerItems,
            worldData,
            checked((uint)casterSession.Character.MinHitPoints),
            prestigeLevel: 0);
        var basePhysicalDamage = attackerFields[LegacyPlayerStatusPacket.MinAttackIndex];
        var expectedHitDamage = basePhysicalDamage * 75U / 100U / 2U;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: TripleAttackPhysicalSpellsSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(TripleAttackPhysicalSpellsSpellId, spell.SpellId);
            Assert.Equal(3, spell.Targets.Count);
            Assert.All(spell.Targets, target =>
            {
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.Equal(expectedHitDamage, target.Damage);
                Assert.NotEqual(1U, target.Damage);
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        var totalDamage = expectedHitDamage * 3UL;
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)totalDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("WorldServer handled legacy PhysicalSpells player magic", log.ToString());
        Assert.DoesNotContain("unsupported player magic sort", log.ToString());
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerPhysicalSpellsPlayerStatusDefenderPreemptsWildPhoenixAndEnqueuesZeroDamageTargets(
        int statusFlag)
    {
        const uint expectedHitDamage = 1U / 2U;
        Assert.Equal(0U, expectedHitDamage);

        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateTripleAttackPhysicalSpellsRow(),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [TripleAttackPhysicalSpellsSpellId],
            legacyRandomSpell: TripleAttackPhysicalSpellsSpellId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "StatusDefendedTarget",
            x: 832,
            y: 584,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(statusFlag, LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: TripleAttackPhysicalSpellsSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(TripleAttackPhysicalSpellsSpellId, spell.SpellId);
            Assert.Equal(3, spell.Targets.Count);
            Assert.All(spell.Targets, target =>
            {
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.Equal(expectedHitDamage, target.Damage);
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "2",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, statusFlag));
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        Assert.DoesNotContain(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy PhysicalSpells player magic", output);
        Assert.Contains("targets=3", output);
        Assert.Contains("totalDamage=0", output);
        Assert.DoesNotContain("WorldServer applied legacy WildPhoenix defender charge", output);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsTripleAttackDoesNotTriggerDormantLineWrathFollowUp()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateTripleAttackPhysicalSpellsRow(),
            CreateWrathOfTheEmperorRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [TripleAttackPhysicalSpellsSpellId, WrathOfTheEmperorSpellId],
            legacyRandomSpell: TripleAttackPhysicalSpellsSpellId);
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
            ReadFrame(new MsgAttackPacket(
                CreateMagicQuery(monster, legacyEncodedQuery: false, TripleAttackPhysicalSpellsSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(TripleAttackPhysicalSpellsSpellId, spell.SpellId);
        Assert.All(spell.Targets, target => Assert.Equal(monster.Uid, target.Uid));
        Assert.DoesNotContain(ParseSpellAnimations(sent), animation => animation.SpellId == WrathOfTheEmperorSpellId);
        Assert.DoesNotContain(WrathOfTheEmperorSpellId.ToString(CultureInfo.InvariantCulture), log.ToString());
    }

    [Theory]
    [InlineData(AlternateWindstormPhysicalSpellsSpellId)]
    [InlineData(AlternateTripleAttackPhysicalSpellsSpellId)]
    public async Task HandlerPhysicalSpellsAdditionalSourceSort55RowsUseDirectThreeHitRouteOnly(
        ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateAdditionalPhysicalSpellsRow(spellId),
            CreateBloomOfDeathRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [spellId, BloomOfDeathSpellId],
            legacyRandomSpell: spellId);
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
            ReadFrame(new MsgAttackPacket(
                CreateMagicQuery(monster, legacyEncodedQuery: false, spellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(spellId, spell.SpellId);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(monster.Uid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.DoesNotContain(ParseSpellAnimations(sent), animation => animation.SpellId == BloomOfDeathSpellId);
        Assert.DoesNotContain("WorldServer handled legacy BloomOfDeath follow-up", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormIgnoresSourceDamageOnHumanFlagAndCarriesSoulLevel()
    {
        const byte learnedSoulLevel = 2;

        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var baselineWorldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateWindstormPhysicalSpellsRow(damageOnHuman: 0));
        var sourceFlagWorldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateWindstormPhysicalSpellsRow(damageOnHuman: 42));

        var baseline = await RunWindstormPhysicalSpellsAnimationAsync(
            baselineWorldData,
            monsterFamily,
            spawn,
            learnedSoulLevel);
        var sourceFlagged = await RunWindstormPhysicalSpellsAnimationAsync(
            sourceFlagWorldData,
            monsterFamily,
            spawn,
            learnedSoulLevel);

        Assert.Equal(WindstormSpellId, sourceFlagged.SpellId);
        Assert.Equal(0, sourceFlagged.SpellEffect);
        Assert.Equal(learnedSoulLevel, sourceFlagged.LevelHu);
        Assert.Equal(3, sourceFlagged.Targets.Count);
        Assert.Equal(
            baseline.Targets.Select(target => target.Damage),
            sourceFlagged.Targets.Select(target => target.Damage));
        Assert.All(sourceFlagged.Targets, target =>
        {
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });
    }

    private static MagicType CreateTripleAttackPhysicalSpellsRow(ushort useMana = 0, byte useStamina = 16)
    {
        return CreateMagicRow(
            TripleAttackPhysicalSpellsSpellId,
            sort: 55,
            useMana: useMana,
            damage: 0,
            maxTargets: 30,
            range: 0,
            needLevel: 5,
            weaponType: 610_622,
            useStamina: useStamina,
            coldTime: 600);
    }

    private static MagicType CreateWindstormPhysicalSpellsRow(int damageOnHuman)
    {
        return CreateMagicRow(
            WindstormSpellId,
            sort: 55,
            useMana: 0,
            damage: 0,
            maxTargets: 30,
            range: 0,
            needLevel: 5,
            weaponType: 611_671,
            useStamina: 0,
            coldTime: 600,
            damageOnHuman: damageOnHuman);
    }

    private static MagicType CreateAdditionalPhysicalSpellsRow(ushort spellId)
    {
        return CreateMagicRow(
            spellId,
            sort: 55,
            useMana: 0,
            damage: 0,
            maxTargets: 30,
            range: 0,
            needLevel: 5,
            weaponType: spellId == AlternateWindstormPhysicalSpellsSpellId ? 611_671U : 610_622U,
            useStamina: 0,
            coldTime: 600);
    }

    private static MagicType CreateWrathOfTheEmperorRow()
    {
        return CreateMagicRow(
            WrathOfTheEmperorSpellId,
            sort: 21,
            useMana: 0,
            damage: 30_190,
            maxTargets: 2,
            range: 15,
            needLevel: 15,
            weaponType: 622,
            useStamina: 16,
            coldTime: 1_500);
    }

    private static async Task<LegacySpellAnimationPacket> RunWindstormPhysicalSpellsAnimationAsync(
        WorldDataCatalog worldData,
        Monster monsterFamily,
        MonsterSpawn spawn,
        byte learnedSoulLevel)
    {
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    WindstormSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: learnedSoulLevel,
                    UseSpellSoul: learnedSoulLevel),
            ],
            legacyRandomSpell: WindstormSpellId);
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
            ReadFrame(new MsgAttackPacket(
                CreateMagicQuery(monster, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var totalDamage = spell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - (uint)totalDamage, damagedMonster.Hitpoints);
        Assert.Contains("WorldServer handled legacy PhysicalSpells monster magic", log.ToString());
        return spell;
    }
}
