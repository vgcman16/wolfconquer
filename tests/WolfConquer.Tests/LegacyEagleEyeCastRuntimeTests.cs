using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort EagleEyeCastSpellId = 11_030;
    private const ushort AdrenalineRushSpellId = 11_130;

    [Fact]
    public async Task EagleEyeCastArmsCountdownAgainstNormalMonsterAndUsesTheMonsterAsAnimationTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateEagleEyeCastRow(),
            CreateAdrenalineRushRow(rate: 100));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [EagleEyeCastSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateEagleEyeQuery(monster.Uid)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        Assert.Equal(EagleEyeCastSpellId, animation.SpellId);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var updatedMonster));
        Assert.True(updatedMonster.Hitpoints < monster.MaxLife);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("1", updatedSession.Character.Attributes["LegacyEagleEyeCountDown"]);
        Assert.True(updatedSession.Character.Attributes.ContainsKey("LegacyEagleEyeExpiresAtUtc"));
    }

    [Fact]
    public async Task EagleEyeCastConsumesCountdownOnBlackSpottedMonsterAndResetsCooldown()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateEagleEyeCastRow(),
            CreateAdrenalineRushRow(rate: 100));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var now = DateTimeOffset.UtcNow;
        Assert.True(monsterStates.TryMarkBlackSpotUntil(monster, now, now.AddSeconds(30)));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [EagleEyeCastSpellId, AdrenalineRushSpellId],
            attributes: new Dictionary<string, string>
            {
                ["LegacyEagleEyeCountDown"] = "1",
                ["LegacyEagleEyeResponseRoll"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        Assert.True(await InvokeLegacyEagleEyeCastAsync(context, session, CreateEagleEyeQuery(monster.Uid), session.PlayerSpells.Spells[0], CreateEagleEyeRuntimeSpell()));

        var response = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == AdrenalineRushSpellId);
        Assert.Equal(PlayerUid, response.AttackerUid);
        Assert.Equal(PlayerUid, response.AttackedUid);
        Assert.Equal((0, 0), (response.X, response.Y));
        Assert.Equal(PlayerUid, Assert.Single(response.Targets).Uid);

        var eagleEye = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == EagleEyeCastSpellId);
        Assert.Equal(PlayerUid, eagleEye.AttackerUid);
        Assert.Equal(PlayerUid, eagleEye.AttackedUid);
        var consumedTarget = Assert.Single(eagleEye.Targets);
        Assert.Equal(PlayerUid, consumedTarget.Uid);
        Assert.Equal(11_135U, consumedTarget.Damage);
        Assert.Contains(ParseMagicColdTimePackets(sent), packet =>
            packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(EagleEyeCastSpellId, 0)]));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("0", updatedSession.Character.Attributes["LegacyEagleEyeCountDown"]);
    }

    [Fact]
    public async Task EagleEyeCastPhysicallyHitsPlayerAndArmsCountdownWhenPlayerIsNotBlackSpotted()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateEagleEyeCastRow(),
            CreateAdrenalineRushRow(rate: 100));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 1_000,
            spellIds: [EagleEyeCastSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        Assert.True(await InvokeLegacyEagleEyeCastAsync(
            context,
            casterSession,
            CreateEagleEyeQuery(TargetPlayerUid),
            casterSession.PlayerSpells.Spells[0],
            CreateEagleEyeRuntimeSpell()));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal("1", updatedCaster.Character.Attributes["LegacyEagleEyeCountDown"]);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.True(updatedTarget.Character.MinHitPoints < targetSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task EagleEyeCastAlwaysUsesTheSobNpcAnimationTargetWithoutChangingCountdown()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 65_930, x: 832, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [sobNpc],
            [CreateEagleEyeCastRow(), CreateAdrenalineRushRow(rate: 100)]);
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [EagleEyeCastSpellId],
            attributes: new Dictionary<string, string>
            {
                ["LegacyEagleEyeCountDown"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        Assert.True(await InvokeLegacyEagleEyeCastAsync(
            context,
            session,
            CreateEagleEyeQuery(checked((uint)sobNpc.Id)),
            session.PlayerSpells.Spells[0],
            CreateEagleEyeRuntimeSpell()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(PlayerUid, animation.AttackedUid);
        var target = Assert.Single(animation.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(sobNpcStates.GetVisible(sobNpc).Life < sobNpc.Life);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("0", updatedSession.Character.Attributes["LegacyEagleEyeCountDown"]);
        Assert.False(updatedSession.Character.Attributes.ContainsKey("LegacyEagleEyeExpiresAtUtc"));
    }

    [Fact]
    public void EagleEyeCountdownExpiresTwentySecondsAfterItWasArmed()
    {
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["LegacyEagleEyeCountDown"] = "1",
                ["LegacyEagleEyeExpiresAtUtc"] = "2026-07-10T21:00:20.0000000+00:00",
            });

        Assert.False(WorldServerFrameHandler.TryExpireLegacyEagleEyeCountdown(
            session.Character,
            DateTimeOffset.Parse("2026-07-10T21:00:19.0000000+00:00"),
            out _));
        Assert.True(WorldServerFrameHandler.TryExpireLegacyEagleEyeCountdown(
            session.Character,
            DateTimeOffset.Parse("2026-07-10T21:00:20.0000000+00:00"),
            out var expiredCharacter));
        Assert.Equal("0", expiredCharacter.Attributes["LegacyEagleEyeCountDown"]);
        Assert.False(expiredCharacter.Attributes.ContainsKey("LegacyEagleEyeExpiresAtUtc"));
    }

    [Fact]
    public async Task MonsterResponseAdrenalineRushSendsSelfAnimationClearsColdTimeAndConsumesCountdown()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateAdrenalineRushRow(rate: 100));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [AdrenalineRushSpellId],
            attributes: new Dictionary<string, string>
            {
                ["LegacyEagleEyeCountDown"] = "1",
                ["LegacyEagleEyeResponseRoll"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        var method = typeof(WorldPacketHandlers).GetMethod(
            "TrySendLegacyMonsterEagleEyeCountdownAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = Assert.IsType<ValueTask<WorldSession>>(method.Invoke(
            null,
            [context, session, CancellationToken.None]));
        var updatedSession = await invocation;

        var response = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, response.AttackerUid);
        Assert.Equal(PlayerUid, response.AttackedUid);
        Assert.Equal(AdrenalineRushSpellId, response.SpellId);
        var target = Assert.Single(response.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal((uint)AdrenalineRushSpellId, target.Damage);
        Assert.False(target.Hit);
        Assert.Contains(ParseMagicColdTimePackets(sent), packet =>
            packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(EagleEyeCastSpellId, 0)]));
        Assert.Equal("0", updatedSession.Character.Attributes["LegacyEagleEyeCountDown"]);
    }

    private static async ValueTask<bool> InvokeLegacyEagleEyeCastAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerEagleEyeMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = Assert.IsType<ValueTask<bool>>(method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]));
        return await invocation;
    }

    private static MsgAttackQuery CreateEagleEyeQuery(uint opponentUid)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
            X: 832,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: EagleEyeCastSpellId);
    }

    private static LegacyMagicRuntimeSpell CreateEagleEyeRuntimeSpell()
    {
        return new LegacyMagicRuntimeSpell(
            EagleEyeCastSpellId,
            LegacyMagicSort.DirectAttack,
            "EagleEye",
            Level: 0,
            UseMana: 0,
            Damage: 100,
            Experience: 0,
            MaxTargets: 0,
            Range: 9,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);
    }

    private static MagicType CreateEagleEyeCastRow()
    {
        var row = CreateMagicRow(EagleEyeCastSpellId, sort: 48, useMana: 0, damage: 100, range: 9, coolDown: 0, coldTime: 0);
        var fields = row.Fields.ToArray();
        fields[3] = "EagleEye";
        return row with { Name = "EagleEye", Fields = fields };
    }

    private static MagicType CreateAdrenalineRushRow(byte rate)
    {
        var row = CreateMagicRow(AdrenalineRushSpellId, sort: 59, useMana: 0, coolDown: 0, coldTime: 0);
        var fields = row.Fields.ToArray();
        fields[3] = "AdrenalineRush";
        fields[12] = rate.ToString();
        return row with { Name = "AdrenalineRush", Fields = fields };
    }
}
