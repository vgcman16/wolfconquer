using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort NebulousHuntSpellId = 10_990;

    [Fact]
    public async Task HandlerNebulousHuntSectorBackHitsMixedLineTargetsWithSourceDamageAndSingleAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var edgeWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 586);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 834, boundY: 587);
        var rangeEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 841, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 60_990, x: 836, y: 586);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, edgeWidthSpawn, outsideWidthSpawn, rangeEdgeSpawn],
            [sobNpc],
            [
                CreateNebulousHuntRow(experience: 1),
                CreateNebulousHuntRow(level: 1, experience: 100),
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
            spellIds: [NebulousHuntSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586,
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
            spellId: NebulousHuntSpellId,
            x: 842,
            y: 584);
        var (_, spell) = ResolveNebulousHuntSpell(casterSession, worldData);
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
            7U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

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
            Assert.Equal(NebulousHuntSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.Equal(MsgAttackEffect.None, Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid).Effect);
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, edgeWidthMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage * 2, damageByUid[onLineMonster.Uid]);
        Assert.Equal(expectedMonsterDamage * 2, damageByUid[edgeWidthMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage * 2, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());
        Assert.False(Assert.Single(casterSpell.Targets, target => target.Uid == TargetPlayerUid).Hit);
        Assert.All(casterSpell.Targets.Where(target => target.Uid != TargetPlayerUid), target => Assert.True(target.Hit));

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeWidthMonster, DateTimeOffset.UtcNow, out var visibleEdgeWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(rangeEdgeMonster, DateTimeOffset.UtcNow, out var visibleRangeEdge));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, visibleOnLine.Hitpoints);
        Assert.Equal(edgeWidthMonster.MaxLife - expectedMonsterDamage, visibleEdgeWidth.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(rangeEdgeMonster.MaxLife, visibleRangeEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(NebulousHuntSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy NebulousHunt SectorBack magic", output);
        Assert.Contains("spellId=10990", output);
        Assert.Contains("sort=69", output);
        Assert.Contains("range=10", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage * 2UL}", output);
        Assert.Contains("source=SectorBack.cs case 10990", output);
    }

    [Fact]
    public async Task HandlerNebulousHuntSectorBackSendsEmptyAnimationWhenLineHasNoTargets()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 850, boundY: 590);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateNebulousHuntRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [NebulousHuntSpellId]);
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
                spellId: NebulousHuntSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(NebulousHuntSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.Contains("source=SectorBack.cs case 10990", log.ToString());
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveNebulousHuntSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            NebulousHuntSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateNebulousHuntRow(
        byte level = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            NebulousHuntSpellId,
            sort: 69,
            level: level,
            useMana: 0,
            damage: 30_500,
            experience: experience,
            maxTargets: 15,
            range: 10,
            weaponType: 61_606_606,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            duration: 25_025,
            damageOnHuman: 25_025,
            damage2: 30_100,
            damage3: 25_025,
            damageOnMonster: 0);
    }
}
