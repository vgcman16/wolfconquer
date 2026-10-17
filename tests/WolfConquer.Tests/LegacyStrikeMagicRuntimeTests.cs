using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort GunfireStrikeSpellId = 14_590;

    [Fact]
    public async Task HandlerGunfireStrikeAgainstMonsterUsesCasterAsAttackedUidAndAppliesPhysicalDamage()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateGunfireStrikeRow(useMana: 9, useStamina: 4));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [GunfireStrikeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var (_, spell) = ResolveGunfireStrikeSpell(session, worldData);
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            session,
            worldData,
            monster,
            monsterFamily,
            spell);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGunfireStrikeQuery(
                monster.Uid,
                monster.X,
                monster.Y)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(monster.X, animation.X);
        Assert.Equal(monster.Y, animation.Y);
        Assert.Equal(GunfireStrikeSpellId, animation.SpellId);
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

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Strike/Gunfire magic", output);
        Assert.Contains("spellId=14590", output);
        Assert.Contains("targetKind=monster", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("source=Strike.cs case 14590", output);
    }

    [Fact]
    public async Task HandlerGunfireStrikeWithoutVisibleTargetStillSpendsResourcesAndSendsEmptyAnimation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateGunfireStrikeRow(useMana: 9, useStamina: 4));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [GunfireStrikeSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateGunfireStrikeQuery(
                opponentUid: 999_999,
                x: 840,
                y: 590)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(590, animation.Y);
        Assert.Equal(GunfireStrikeSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(491, updatedSession.Character.MinMana);
        Assert.Equal("96", updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains("targetKind=none targets=0", log.ToString());
    }

    [Fact]
    public async Task HandlerGunfireStrikeAgainstPlayerAppliesSourceTwoThirdsPhysicalDamage()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateGunfireStrikeRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [GunfireStrikeSpellId]);
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
        var (_, spell) = ResolveGunfireStrikeSpell(casterSession, worldData);
        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedDamage = checked(baselineDamage * 2 / 3);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGunfireStrikeQuery(
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
            Assert.Equal(GunfireStrikeSpellId, animation.SpellId);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("targetKind=player targets=1", log.ToString());
    }

    private static MagicType CreateGunfireStrikeRow(ushort useMana = 0, byte useStamina = 0)
    {
        return CreateMagicRow(
            GunfireStrikeSpellId,
            sort: 81,
            useMana: useMana,
            damage: 120,
            range: 9,
            useStamina: useStamina,
            coldTime: 0);
    }

    private static MsgAttackQuery CreateGunfireStrikeQuery(uint opponentUid, ushort x, ushort y)
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
            SpellId: GunfireStrikeSpellId);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell)
        ResolveGunfireStrikeSpell(WorldSession session, WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            GunfireStrikeSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }
}
