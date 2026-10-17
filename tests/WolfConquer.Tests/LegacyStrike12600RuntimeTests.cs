using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort Strike12600SpellId = 12_600;

    [Fact]
    public async Task HandlerStrike12600AgainstMonsterDoublesPhysicalSoulDamageAndAppliesProgress()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateStrike12600Row(useMana: 9, useStamina: 4));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [Strike12600SpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var (_, spell) = ResolveStrikeSpell(session, worldData, Strike12600SpellId);
        var baseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            session,
            worldData,
            monster,
            monsterFamily,
            spell);
        var expectedDamage = unchecked(baseDamage * 2U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateStrikeQuery(
                Strike12600SpellId,
                monster.Uid,
                monster.X,
                monster.Y)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(Strike12600SpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife - expectedDamage, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(491, updatedSession.Character.MinMana);
        Assert.Equal("96", updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains("targetKind=monster targets=1", log.ToString());
        Assert.Contains("source=Strike.cs case 12600", log.ToString());
    }

    [Fact]
    public async Task HandlerStrike12600AgainstPlayerAppliesWildPhoenixOrderedTwoThirdsDamageWithoutProgress()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateStrike12600Row());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [Strike12600SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
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
            Sessions: sessions);
        var (_, spell) = ResolveStrikeSpell(casterSession, worldData, Strike12600SpellId);
        var baseDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedDamage = checked(baseDamage * 2U / 3U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateStrikeQuery(
                Strike12600SpellId,
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y))).ToFrameBytes()));

        foreach (var animation in new[]
                 {
                     Assert.Single(ParseSpellAnimations(casterSent)),
                     Assert.Single(ParseSpellAnimations(targetSent)),
                 })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(PlayerUid, animation.AttackedUid);
            Assert.Equal(Strike12600SpellId, animation.SpellId);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        Assert.Contains("targetKind=player targets=1", log.ToString());
    }

    [Fact]
    public async Task HandlerStrike12600AgainstSobNpcQuadruplesPhysicalSoulDamageAndAppliesProgress()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 65_360, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateStrike12600Row()]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [Strike12600SpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);
        var (_, spell) = ResolveStrikeSpell(session, worldData, Strike12600SpellId);
        var baseDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            session,
            spell,
            worldData,
            1U);
        var expectedDamage = unchecked(baseDamage * 4U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateStrikeQuery(
                Strike12600SpellId,
                checked((uint)sobNpc.Id),
                checked((ushort)sobNpc.X),
                checked((ushort)sobNpc.Y))).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(Strike12600SpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(sobNpc.Life - checked((int)expectedDamage), sobNpcStates.GetVisible(sobNpc).Life);
        Assert.Contains("targetKind=sob-npc targets=1", log.ToString());
    }

    [Fact]
    public async Task HandlerStrike12600WithoutVisibleTargetSpendsResourcesAndSendsEmptyAnimation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateStrike12600Row(useMana: 9, useStamina: 4));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [Strike12600SpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateStrikeQuery(
                Strike12600SpellId,
                opponentUid: 999_999,
                x: 840,
                y: 590)).ToFrameBytes()));

        var spellIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(sent[index]), out _));
        var manaUpdateIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana);
        var staminaUpdateIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina);
        Assert.True(manaUpdateIndex < spellIndex);
        Assert.True(staminaUpdateIndex < spellIndex);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(Strike12600SpellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(491, updatedSession.Character.MinMana);
        Assert.Equal("96", updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains("targetKind=none targets=0", log.ToString());
    }

    [Fact]
    public async Task HandlerGunfireStrikeAgainstMonsterRetainsItsUnmultipliedDamage()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateGunfireStrikeRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [GunfireStrikeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var (_, spell) = ResolveStrikeSpell(session, worldData, GunfireStrikeSpellId);
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            session,
            worldData,
            monster,
            monsterFamily,
            spell);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateStrikeQuery(
                GunfireStrikeSpellId,
                monster.Uid,
                monster.X,
                monster.Y)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(GunfireStrikeSpellId, animation.SpellId);
        Assert.Equal(expectedDamage, Assert.Single(animation.Targets).Damage);
        Assert.Contains("source=Strike.cs case 14590", log.ToString());
    }

    private static MagicType CreateStrike12600Row(ushort useMana = 0, byte useStamina = 0)
    {
        return CreateMagicRow(
            Strike12600SpellId,
            sort: 81,
            useMana: useMana,
            damage: 100,
            range: 8,
            useStamina: useStamina,
            coldTime: 0);
    }

    private static MsgAttackQuery CreateStrikeQuery(ushort spellId, uint opponentUid, ushort x, ushort y)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolveStrikeSpell(WorldSession session, WorldDataCatalog worldData, ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }
}
