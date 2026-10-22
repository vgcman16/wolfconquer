using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort LegacyEagleEyeCountdownSpellId = 11_130;
    private const int LegacyMagicRateFieldIndex = 12;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerWithAttackerNeptuneCurseLeavesTargetAtOneHitpoint()
    {
        const int startingHitpoints = 900;
        const uint expectedDamage = startingHitpoints - 1;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyNeptuneCurseFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: startingHitpoints);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

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
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(1, updatedTargetSession.Character.MinHitPoints);

        foreach (var spell in new[]
        {
            Assert.Single(ParseSpellAnimations(casterSent)),
            Assert.Single(ParseSpellAnimations(targetSent)),
        })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.NotEqual(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains("attacker NeptuneCurse player-hit damage cap", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerSetsAttackerAttackHit()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

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
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("true", updatedCasterSession.Character.Attributes[RuntimeLegacyAttackHitAttribute]);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerWithTargetEagleEyeCountdownSendsSelfAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateMagicRowWithRate(LegacyEagleEyeCountdownSpellId, sort: 0, rate: 100));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            spellEntries:
            [
                CreatePlayerSpell(LegacyEagleEyeCountdownSpellId),
            ],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyEagleEyeCountDown"] = "1",
                ["LegacyEagleEyeResponseRoll"] = "0",
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

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
                SpellId: ThunderSpellId)).ToFrameBytes()));

        var targetAnimations = ParseSpellAnimations(targetSent);
        var normalAttack = Assert.Single(targetAnimations, spell => spell.SpellId == ThunderSpellId);
        Assert.Equal(PlayerUid, normalAttack.AttackerUid);
        var normalTarget = Assert.Single(normalAttack.Targets);
        Assert.Equal(TargetPlayerUid, normalTarget.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, normalTarget.Damage);

        var eagleEye = Assert.Single(targetAnimations, spell => spell.SpellId == LegacyEagleEyeCountdownSpellId);
        Assert.Equal(TargetPlayerUid, eagleEye.AttackerUid);
        Assert.Equal(TargetPlayerUid, eagleEye.AttackedUid);
        Assert.Equal(0, eagleEye.X);
        Assert.Equal(0, eagleEye.Y);
        var eagleEyeTarget = Assert.Single(eagleEye.Targets);
        Assert.Equal(TargetPlayerUid, eagleEyeTarget.Uid);
        Assert.Equal(LegacyEagleEyeCountdownSpellId, eagleEyeTarget.Damage);
        Assert.False(eagleEyeTarget.Hit);

        Assert.Single(ParseSpellAnimations(casterSent), spell => spell.SpellId == ThunderSpellId);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal("1", updatedTargetSession.Character.Attributes["LegacyEagleEyeCountDown"]);
    }

    private static MagicType CreateMagicRowWithRate(ushort spellId, byte sort, byte rate)
    {
        var row = CreateMagicRow(spellId, sort, useMana: 0, coldTime: 0);
        var fields = row.Fields.ToArray();
        fields[LegacyMagicRateFieldIndex] = rate.ToString();
        return row with { Fields = fields };
    }
}
