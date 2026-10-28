using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort MortalStrikeSpellId = 11_990;

    [Fact]
    public async Task HandlerMortalStrikeSectorBackHitsLineTargetsAndSendsSourcePerTargetAnimations()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 832, boundY: 584);
        var expandedWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 833, boundY: 587);
        var outsideRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 836, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 61_990, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, expandedWidthSpawn, outsideRangeSpawn],
            [sobNpc],
            [
                CreateMortalStrikeRow(experience: 1),
                CreateMortalStrikeRow(level: 1, experience: 100),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var expandedWidthMonster = CreateRuntimeMonsterInstance(worldData, expandedWidthSpawn, monsterFamily);
        var outsideRangeMonster = CreateRuntimeMonsterInstance(worldData, outsideRangeSpawn, monsterFamily);
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
            spellIds: [MortalStrikeSpellId],
            legacyRandomSpell: MortalStrikeSpellId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "MortalStrikeTarget",
            x: 832,
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;
        var query = CreateMagicQuery(
            onLineMonster,
            legacyEncodedQuery: false,
            spellId: MortalStrikeSpellId,
            x: 835,
            y: 584);
        var (_, spell) = ResolveMortalStrikeSpell(casterSession, worldData);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 10 / 16;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        Assert.Equal(5, spell.Range);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterAnimations = ParseSpellAnimations(casterSent).ToArray();
        var targetAnimations = ParseSpellAnimations(targetSent).ToArray();
        Assert.Equal(4, casterAnimations.Length);
        Assert.Equal(
            casterAnimations.Select(animation => animation.Targets.Single().Uid).Order().ToArray(),
            targetAnimations.Select(animation => animation.Targets.Single().Uid).Order().ToArray());
        Assert.All(casterAnimations, animation =>
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(835, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(MortalStrikeSpellId, animation.SpellId);
            Assert.Single(animation.Targets);
        });

        var damageByUid = casterAnimations.ToDictionary(
            animation => animation.Targets.Single().Uid,
            animation => animation.Targets.Single());
        Assert.Equal(
            new[] { onLineMonster.Uid, expandedWidthMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage, damageByUid[onLineMonster.Uid].Damage);
        Assert.True(damageByUid[onLineMonster.Uid].Hit);
        Assert.Equal(expectedMonsterDamage, damageByUid[expandedWidthMonster.Uid].Damage);
        Assert.True(damageByUid[expandedWidthMonster.Uid].Hit);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid].Damage);
        Assert.False(damageByUid[TargetPlayerUid].Hit);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)].Damage);
        Assert.True(damageByUid[checked((uint)sobNpc.Id)].Hit);

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(expandedWidthMonster, DateTimeOffset.UtcNow, out var visibleExpandedWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRangeMonster, DateTimeOffset.UtcNow, out var visibleOutsideRange));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, visibleOnLine.Hitpoints);
        Assert.Equal(expandedWidthMonster.MaxLife - expectedMonsterDamage, visibleExpandedWidth.Hitpoints);
        Assert.Equal(outsideRangeMonster.MaxLife, visibleOutsideRange.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(MortalStrikeSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy MortalStrike SectorBack magic", output);
        Assert.Contains("spellId=11990", output);
        Assert.Contains("sort=69", output);
        Assert.Contains("range=5", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("source=SectorBack.cs case 11990", output);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveMortalStrikeSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            MortalStrikeSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateMortalStrikeRow(
        byte level = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            MortalStrikeSpellId,
            sort: 69,
            level: level,
            useMana: 0,
            damage: 30_195,
            experience: experience,
            maxTargets: 5,
            range: 5,
            needLevel: 15,
            weaponType: 61_614_614,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            duration: 0,
            damageOnHuman: 30_200,
            damage2: 0,
            damage3: 0,
            damageOnMonster: 35);
    }
}
