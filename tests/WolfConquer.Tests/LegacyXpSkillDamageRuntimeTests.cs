using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task BladeFlurryWithoutXpListHitsVisibleMonsterPlayerAndSobInsideInclusiveCasterRange()
    {
        var monsterFamily = CreateMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584);
        var boundarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 837, boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_610, x: 836, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn, boundarySpawn, outsideSpawn],
            [sobNpc],
            [
                CreateSpecialXpSkillDamageRow(TestBladeFlurrySpellId, sort: 68, range: 7),
                CreateSpecialXpSkillDamageRow(TestBladeFlurrySpellId, sort: 68, level: 1, range: 7, experience: 100),
            ]);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundarySpawn, monsterFamily);
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
            minMana: 50,
            strength: 300,
            spellIds: [TestBladeFlurrySpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 835,
            y: 584,
            minHitPoints: 5_000);
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                inRangeMonster,
                legacyEncodedQuery: false,
                spellId: TestBladeFlurrySpellId,
                x: 840,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(840, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(TestBladeFlurrySpellId, spell.SpellId);

        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { inRangeMonster.Uid, boundaryMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.DoesNotContain(outsideMonster.Uid, damageByUid.Keys);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(inRangeMonster.MaxLife - damageByUid[inRangeMonster.Uid], damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(boundaryMonster, DateTimeOffset.UtcNow, out var damagedBoundaryMonster));
        Assert.Equal(boundaryMonster.MaxLife - damageByUid[boundaryMonster.Uid], damagedBoundaryMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideMonster, DateTimeOffset.UtcNow, out var untouchedOutsideMonster));
        Assert.Equal(outsideMonster.MaxLife, untouchedOutsideMonster.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)damageByUid[TargetPlayerUid], updatedTargetSession.Character.MinHitPoints);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)damageByUid[checked((uint)sobNpc.Id)], visibleSobNpc.Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(43, updatedCasterSession.Character.MinMana);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(TestBladeFlurrySpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy BladeFlurry non-XP special area damage", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("source=BladeFlurry.cs non-XP branch", output);
        Assert.DoesNotContain("unsupported-magic", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    [Fact]
    public async Task OmnipotenceWithoutXpListHitsVisibleMonsterPlayerAndSobInsideStrictCasterRange()
    {
        var monsterFamily = CreateMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584);
        var boundarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 837, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_860, x: 836, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn, boundarySpawn],
            [sobNpc],
            [
                CreateSpecialXpSkillDamageRow(TestOmnipotenceSpellId, sort: 85, range: 7),
                CreateSpecialXpSkillDamageRow(TestOmnipotenceSpellId, sort: 85, level: 1, range: 7, experience: 100),
            ]);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundarySpawn, monsterFamily);
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
            minMana: 50,
            strength: 300,
            spellIds: [TestOmnipotenceSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 835,
            y: 584,
            minHitPoints: 5_000);
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                inRangeMonster,
                legacyEncodedQuery: false,
                spellId: TestOmnipotenceSpellId,
                x: 840,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(840, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(TestOmnipotenceSpellId, spell.SpellId);

        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { inRangeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.DoesNotContain(boundaryMonster.Uid, damageByUid.Keys);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(inRangeMonster.MaxLife - damageByUid[inRangeMonster.Uid], damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(boundaryMonster, DateTimeOffset.UtcNow, out var untouchedBoundaryMonster));
        Assert.Equal(boundaryMonster.MaxLife, untouchedBoundaryMonster.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)damageByUid[TargetPlayerUid], updatedTargetSession.Character.MinHitPoints);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)damageByUid[checked((uint)sobNpc.Id)], visibleSobNpc.Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(43, updatedCasterSession.Character.MinMana);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(TestOmnipotenceSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Omnipotence non-XP special area damage", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("source=Omnipotence.cs non-XP branch", output);
        Assert.DoesNotContain("unsupported-magic", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    private static MagicType CreateSpecialXpSkillDamageRow(
        ushort spellId,
        byte sort,
        byte level = 0,
        byte range = 7,
        uint experience = 0)
    {
        return CreateMagicRow(
            spellId,
            sort,
            level: level,
            useMana: 7,
            damage: 120,
            experience: experience,
            range: range,
            coldTime: 0);
    }
}
