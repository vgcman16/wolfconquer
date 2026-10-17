using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ThunderStrikerLineSpellId = 15_810;
    private const int ThunderStrikerLineWildPhoenixFlag = 0x170;

    [Fact]
    public void CalculateThunderStrikerLinePlayerTargetUsesDecompiledFixedNinetySevenThenDamageOnHumanPercent()
    {
        const ushort fixedPercentSpellId = 31_008;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ThunderStrikerLineSpellId, fixedPercentSpellId],
            classCode: 9_001);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 585);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateThunderStrikerLineRow(damageOnHuman: 30_100));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateThunderStrikerLineRow(damageOnHuman: 30_200));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateThunderStrikerLineRow(spellId: fixedPercentSpellId, damage: 97, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateThunderStrikerLineRow(spellId: fixedPercentSpellId, damage: 194, damageOnHuman: 0));
        var (_, baselineSpell) = ResolveThunderStrikerLineSpell(attacker, baselineWorldData);
        var (_, boostedSpell) = ResolveThunderStrikerLineSpell(attacker, boostedWorldData);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(100U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(200U, boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Fact]
    public async Task HandlerThunderStrikerLineHitsMixedTargetsWithSourceDamageAndSingleAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var edgeWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 585);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 834, boundY: 586);
        var rangeEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 845, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_810, x: 836, y: 585);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, edgeWidthSpawn, outsideWidthSpawn, rangeEdgeSpawn],
            [sobNpc],
            [
                CreateThunderStrikerLineRow(experience: 1),
                CreateThunderStrikerLineRow(level: 1, experience: 100),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var edgeWidthMonster = CreateRuntimeMonsterInstance(worldData, edgeWidthSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var rangeEdgeMonster = CreateRuntimeMonsterInstance(worldData, rangeEdgeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ThunderStrikerLineSpellId],
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 585,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var query = CreateMagicQuery(
            onLineMonster,
            legacyEncodedQuery: false,
            spellId: ThunderStrikerLineSpellId,
            x: 842,
            y: 584);
        var (_, spell) = ResolveThunderStrikerLineSpell(casterSession, worldData);
        var expectedMonsterRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedMonsterAnimationDamage = expectedMonsterRuntimeDamage * 2;
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 53 / 100;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U) * 2;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(ThunderStrikerLineSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target =>
            {
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, edgeWidthMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterAnimationDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(expectedMonsterAnimationDamage, damageByUid[edgeWidthMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeWidthMonster, DateTimeOffset.UtcNow, out var visibleEdgeWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(rangeEdgeMonster, DateTimeOffset.UtcNow, out var visibleRangeEdge));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterRuntimeDamage, visibleOnLine.Hitpoints);
        Assert.Equal(edgeWidthMonster.MaxLife - expectedMonsterRuntimeDamage, visibleEdgeWidth.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(rangeEdgeMonster.MaxLife, visibleRangeEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("85", updatedCasterSession.Character.Attributes["Stamina"]);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(ThunderStrikerLineSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ThunderStriker Line magic", output);
        Assert.Contains("range=11", output);
        Assert.Contains("width=1", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterRuntimeDamage * 2UL}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcDamage}", output);
        Assert.Contains("source=Line.cs case 15810", output);
    }

    [Fact]
    public async Task HandlerThunderStrikerLineAgainstWildPhoenixPlayerConsumesDefenderChargeAndScalesDamageToZero()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateThunderStrikerLineRow(),
                CreateMagicRow(
                    WildPhoenixSpellId,
                    sort: 6,
                    useMana: 0,
                    damage: 4,
                    coldTime: 0,
                    duration: 32,
                    damageOnHuman: 18,
                    damage2: 7),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ThunderStrikerLineSpellId],
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 840,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(ThunderStrikerLineWildPhoenixFlag),
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
        var expectedPlayerDamage = 1U * 53 / 100;
        Assert.Equal(0U, expectedPlayerDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderStrikerLineSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(ThunderStrikerLineSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, ThunderStrikerLineWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == ThunderStrikerLineWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy ThunderStriker Line magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("source=Line.cs case 15810", output);
    }

    [Fact]
    public async Task HandlerThunderStrikerLineSkipsGuardMonstersButStillHitsPlayerAndSobTargets()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            boundX: 833,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var sobNpc = CreateMoveLineSobNpc(id: 65_811, x: 836, y: 585);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            [sobNpc],
            [CreateThunderStrikerLineRow()]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ThunderStrikerLineSpellId],
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 585,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var query = CreateMagicQuery(
            guard,
            legacyEncodedQuery: false,
            spellId: ThunderStrikerLineSpellId,
            x: 842,
            y: 584);
        var (_, spell) = ResolveThunderStrikerLineSpell(casterSession, worldData);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 53 / 100;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U) * 2;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);

        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife, visibleGuard.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);
    }

    [Fact]
    public async Task HandlerThunderStrikerLineNonThunderStrikerSpendsResourcesThenReturnsWithoutAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateThunderStrikerLineRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ThunderStrikerLineSpellId],
            classCode: 142);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = CreateMagicQuery(
            monster,
            legacyEncodedQuery: false,
            spellId: ThunderStrikerLineSpellId,
            x: 842,
            y: 584);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("85", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("WorldServer source-backed returned legacy ThunderStriker Line magic", output);
        Assert.Contains("spellId=15810", output);
        Assert.Contains("class=142", output);
        Assert.Contains("AtributesStatus.IsThunderStriker is false", output);
    }

    private static uint InvokePrivateUInt32(string methodName, params object?[] arguments)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, arguments);
        return Assert.IsType<uint>(result);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveThunderStrikerLineSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            ThunderStrikerLineSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateThunderStrikerLineRow(
        ushort spellId = ThunderStrikerLineSpellId,
        byte level = 0,
        uint experience = 0,
        uint damage = 30_300U,
        int damageOnHuman = 30_110)
    {
        return CreateMagicRow(
            spellId,
            sort: 14,
            level: level,
            useMana: 0,
            damage: damage,
            experience: experience,
            maxTargets: 11,
            range: 11,
            weaponType: 61_681_680,
            useStamina: 15,
            coolDown: 600,
            coldTime: 0,
            damageOnHuman: damageOnHuman);
    }
}
