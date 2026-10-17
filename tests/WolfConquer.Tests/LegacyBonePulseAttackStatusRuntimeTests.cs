using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort BonePulseAttackStatusSpellId = 16_390;
    private const int BonePulseStatusFlag = 0x11F;

    [Fact]
    public async Task HandlerBonePulse16390SelfStatusSetsFlagPowerAndSourceAnimation()
    {
        const int bonePulsePower = 50;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                BonePulseAttackStatusSpellId,
                sort: 16,
                useMana: 0,
                coldTime: 0,
                damageOnHuman: bonePulsePower));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [BonePulseAttackStatusSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateSelfStatusSetterQuery(BonePulseAttackStatusSpellId, session)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, BonePulseStatusFlag));
        Assert.Equal(
            bonePulsePower.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["BonePulsePower"]);
        Assert.Equal(
            bonePulsePower.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["LegacyBonePulsePower"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyBonePulseRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyBonePulseExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: 10);

        var sentSnapshot = sent.ToArray();
        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(0, spell.X);
        Assert.Equal(0, spell.Y);
        Assert.Equal(BonePulseAttackStatusSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var statusUpdate = Assert.Single(
            ParseMsgUpdates(sentSnapshot),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, BonePulseStatusFlag));

        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, BonePulseStatusFlag)));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == BonePulseAttackStatusSpellId);
        Assert.True(statusIndex < animationIndex);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy BonePulse AttackStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 16390", output);
    }

    [Fact]
    public async Task HandlerAttackStatus7000PlayerHitUsesAttackerBonePulseToBypassTargetTortoiseAndMarkEffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                7_000,
                sort: 16,
                useMana: 0,
                damage: 100,
                range: 7,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 1_000,
            spellIds: [7_000],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(BonePulseStatusFlag),
                ["BonePulsePower"] = "50",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "TortoiseTarget",
            x: 835,
            y: 584,
            minHitPoints: 10_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyTortoiseDamagePercent"] = "20",
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
                SpellId: 7_000)).ToFrameBytes()));

        const uint expectedDamage = 900;
        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.BonePulseDestroy, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(10_000 - expectedDamage, updatedTargetSession.Character.MinHitPoints);
    }
}
