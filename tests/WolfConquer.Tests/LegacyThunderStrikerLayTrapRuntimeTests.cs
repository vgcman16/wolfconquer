using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ThunderStrikerLayTrapSpellId = 15_770;
    private const string ThunderStrikerEnergyAttribute = "ThunderStrikerEnergy";

    [Fact]
    public void CalculateThunderStrikerLayTrapPlayerTargetUsesMatchingDecompileDamageOnHumanPercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ThunderStrikerLayTrapSpellId],
            classCode: 9_001);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 839,
            y: 584,
            minHitPoints: 100_000);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateThunderStrikerLayTrapRow(damageOnHuman: 30_110));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateThunderStrikerLayTrapRow(damageOnHuman: 30_150));
        var (_, baselineSpell) = ResolveThunderStrikerLayTrapSpell(attacker, baselineWorldData);
        var (_, boostedSpell) = ResolveThunderStrikerLayTrapSpell(attacker, boostedWorldData);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(110U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(150U, boostedSpell.DamageOnHuman % 1_000);

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

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected matching-decompile SkyFall Physical.OnPlayer damage percent to use DamageOnHuman. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public async Task HandlerThunderStrikerLayTrapSkyFallSpendsAttributeEnergyAndHitsRadiusAtCastPoint()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var centerSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584);
        var edgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 840, boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 841, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_770, x: 838, y: 586);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [centerSpawn, edgeSpawn, outsideSpawn],
            [sobNpc],
            [
                CreateThunderStrikerLayTrapRow(experience: 1),
                CreateThunderStrikerLayTrapRow(level: 1, experience: 100),
            ]);
        var centerMonster = CreateRuntimeMonsterInstance(worldData, centerSpawn, monsterFamily);
        var edgeMonster = CreateRuntimeMonsterInstance(worldData, edgeSpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [ThunderStrikerLayTrapSpellId],
            attributes: new Dictionary<string, string>
            {
                [ThunderStrikerEnergyAttribute] = "150",
            },
            classCode: 9_001);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 839,
            y: 584,
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
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            ThunderStrikerLayTrapSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            centerMonster,
            monsterFamily,
            spell) * 5;
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 18 / 28;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U) * 3;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                centerMonster,
                legacyEncodedQuery: false,
                spellId: ThunderStrikerLayTrapSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(836, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(ThunderStrikerLayTrapSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            AssertBombEqualsOneIfParserSupportsIt(animation);
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { centerMonster.Uid, edgeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage, damageByUid[centerMonster.Uid]);
        Assert.Equal(expectedMonsterDamage, damageByUid[edgeMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(centerMonster, DateTimeOffset.UtcNow, out var visibleCenter));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeMonster, DateTimeOffset.UtcNow, out var visibleEdge));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, DateTimeOffset.UtcNow, out var visibleOutside));
        Assert.Equal(centerMonster.MaxLife - expectedMonsterDamage, visibleCenter.Hitpoints);
        Assert.Equal(edgeMonster.MaxLife - expectedMonsterDamage, visibleEdge.Hitpoints);
        Assert.Equal(outsideMonster.MaxLife, visibleOutside.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("50", updatedCasterSession.Character.Attributes[ThunderStrikerEnergyAttribute]);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(ThunderStrikerLayTrapSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ThunderStriker LayTrap magic", output);
        Assert.Contains("spellId=15770", output);
        Assert.Contains("radius=4", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"attribute={ThunderStrikerEnergyAttribute}", output);
        Assert.Contains("source=LayTrap.cs case 15770", output);
    }

    [Theory]
    [InlineData(142, 150U, "AtributesStatus.IsThunderStriker is false")]
    [InlineData(9_001, 99U, "ThunderStrikerEnergy=99")]
    public async Task HandlerThunderStrikerLayTrapGateReturnsAfterResourcesWithoutAnimation(
        int classCode,
        uint energy,
        string expectedLog)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateThunderStrikerLayTrapRow(useMana: 7));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            strength: 300,
            spellIds: [ThunderStrikerLayTrapSpellId],
            attributes: new Dictionary<string, string>
            {
                [ThunderStrikerEnergyAttribute] = energy.ToString(CultureInfo.InvariantCulture),
            },
            classCode: classCode);
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
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: ThunderStrikerLayTrapSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal(energy.ToString(CultureInfo.InvariantCulture), updatedSession.Character.Attributes[ThunderStrikerEnergyAttribute]);

        var output = log.ToString();
        Assert.Contains("WorldServer source-backed returned legacy ThunderStriker LayTrap magic", output);
        Assert.Contains("spellId=15770", output);
        Assert.Contains(expectedLog, output);
        Assert.Contains("breaks after CanUseSpell.Verified without animation", output);
    }

    private static MagicType CreateThunderStrikerLayTrapRow(
        byte level = 0,
        uint experience = 0,
        ushort useMana = 0,
        int damageOnHuman = 30_110)
    {
        return CreateMagicRow(
            ThunderStrikerLayTrapSpellId,
            sort: (byte)LegacyMagicSort.LayTrap,
            level: level,
            useMana: useMana,
            damage: 30_200U,
            experience: experience,
            maxTargets: 8,
            range: 15,
            weaponType: 61_681_680,
            coolDown: 0,
            coldTime: 0,
            damageOnHuman: damageOnHuman);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveThunderStrikerLayTrapSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            ThunderStrikerLayTrapSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }
}
