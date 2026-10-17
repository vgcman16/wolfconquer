using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false, 500U, 387U, 0U, 209U)]
    [InlineData(false, 291U, 387U, 0U, 0U)]
    [InlineData(false, 100U, 387U, 191U, 0U)]
    [InlineData(true, 500U, 193U, 0U, 355U)]
    [InlineData(true, 100U, 193U, 45U, 0U)]
    public async Task HandlerHellVortexLightningShieldPublishesAdjustedDamageAndRetainsHitAndEffect(
        bool defensiveStance,
        uint shieldLeft,
        uint expectedRequestedDamage,
        uint expectedReceiveDamage,
        uint expectedShieldLeft)
    {
        // HawksAmbition first: 387 - 96 = 291, or stance 193 - 48 = 145; then shield absorption.
        var result = await RunHellVortexReceiveEnvelopeAsync(
            defensiveStance: defensiveStance,
            lightningShieldLeft: shieldLeft,
            expectedReceiveDamage: expectedReceiveDamage);

        var target = Assert.Single(result.Targets);
        Assert.Equal(expectedRequestedDamage, target.RequestedDamage);
        Assert.Equal(100_000L - expectedReceiveDamage, target.Session.Character.MinHitPoints);
        Assert.Equal(defensiveStance ? MsgAttackEffect.Block : MsgAttackEffect.None, target.Effect);
        AssertHellVortexReceiveEnvelopeShieldBeforeHitpoints(target, expectedShieldLeft);
        AssertHellVortexReceiveEnvelope(result);
    }

    [Theory]
    [InlineData(false, null, 387U, 291U)]
    [InlineData(true, null, 193U, 145U)]
    [InlineData(false, 100U, 387U, 191U)]
    public async Task HandlerHellVortexOverkillAnimationKeepsUncappedReceiveDamage(
        bool defensiveStance,
        uint? shieldLeft,
        uint expectedRequestedDamage,
        uint expectedReceiveDamage)
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(
            defensiveStance: defensiveStance,
            targetHitpoints: 100,
            targetSpirit: 0,
            lightningShieldLeft: shieldLeft,
            expectedReceiveDamage: expectedReceiveDamage);

        var target = Assert.Single(result.Targets);
        Assert.Equal(expectedRequestedDamage, target.RequestedDamage);
        Assert.Equal(100U, target.AppliedDamage);
        Assert.Equal(0, target.Session.Character.MinHitPoints);
        foreach (var frames in new[] { result.CasterSent, target.Sent })
        {
            var hit = Assert.Single(Assert.Single(ParseSpellAnimations(frames)).Targets);
            Assert.Equal(expectedReceiveDamage, hit.Damage);
            Assert.NotEqual(target.AppliedDamage, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(defensiveStance ? MsgAttackEffect.Block : MsgAttackEffect.None, hit.Effect);
        }
        if (shieldLeft is not null)
        {
            AssertHellVortexReceiveEnvelopeShieldBeforeHitpoints(target, expectedShieldLeft: 0);
        }
        AssertHellVortexReceiveEnvelope(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HellVortexDamageReceivePhaseTargetNeptuneZerosCarrierButStillPublishesHitpoints(
        bool attackerNeptuneCurse)
    {
        // Direct receive phase only: CanAttackPlayer.cs:75 rejects an initially cursed spell target.
        // This isolates ReceiveAttack.Player.cs:557-565 -> 799, without bypassing the handler gate.
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(), [],
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0),
            CreateMagicRow(LegacyLightningShieldSpellId, sort: 0, level: 3, useMana: 0, damage: 0, coldTime: 0));
        var caster = CreateSession(spellIds: [HellVortexSpellId], attributes: new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(attackerNeptuneCurse ? [LegacyNeptuneCurseFlag] : []),
            ["HawksAmbitionPower"] = "25",
        });
        var targetSession = CreateSession(
            uid: TargetPlayerUid, name: "ReceivePhaseTarget", x: 840, y: 584, minHitPoints: 100_000,
            spellEntries: [CreatePlayerSpell(LegacyLightningShieldSpellId, level: 3)],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    LegacyNeptuneCurseFlag, HellVortexReceiveEnvelopeHawksFlag, LegacyLightningShieldActivatedFlag),
                ["MaxHitpoints"] = "100000",
                ["LightningShieldLeft"] = "100",
            });
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, targetSession);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection, log, caster, WorldData: worldData, Sessions: sessions,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var receive = typeof(WorldPacketHandlers).GetMethod(
            "ApplyLegacyPlayerMagicDamageToPlayerAsync", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(receive);
        var invocation = receive.Invoke(null,
            [context, new WorldActiveSession(targetConnection, targetSession), caster.Character, 193U,
                CancellationToken.None, false, null]);
        Assert.NotNull(invocation);
        var asTask = invocation.GetType().GetMethod(nameof(ValueTask.AsTask), Type.EmptyTypes);
        Assert.NotNull(asTask);
        var pending = Assert.IsAssignableFrom<Task>(asTask.Invoke(invocation, null));
        await pending;
        var outcome = pending.GetType().GetProperty("Result")!.GetValue(pending);
        Assert.NotNull(outcome);
        Assert.Equal("None", outcome.GetType().GetProperty("AbortReason")!.GetValue(outcome)!.ToString());
        var result = outcome.GetType().GetProperty("Result")!.GetValue(outcome);
        Assert.NotNull(result);
        Assert.Equal(0U, Assert.IsType<uint>(result.GetType().GetProperty("ReceiveDamage")!.GetValue(result)));
        Assert.Equal(0U, Assert.IsType<uint>(result.GetType().GetProperty("DamageApplied")!.GetValue(result)));
        Assert.False(Assert.IsType<bool>(result.GetType().GetProperty("Killed")!.GetValue(result)));
        var updatedTarget = Assert.IsType<WorldSession>(result.GetType().GetProperty("UpdatedSession")!.GetValue(result));
        var updatedCaster = result.GetType().GetProperty("UpdatedAttackerSession")!.GetValue(result) as WorldSession ?? caster;
        Assert.Equal(100_000, updatedTarget.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTarget.Character.Attributes, LegacyNeptuneCurseFlag));
        Assert.False(HasLegacyStatusFlag(updatedTarget.Character.Attributes, HellVortexReceiveEnvelopeHawksFlag));

        var send = typeof(WorldPacketHandlers).GetMethod(
            "SendLegacyPlayerMagicDamageUpdatesAsync", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(send);
        // Respect any optional defaults; the test must not opt into unchanged-HP publication.
        var arguments = Enumerable.Repeat<object?>(Type.Missing, send.GetParameters().Length).ToArray();
        arguments[0] = context with { Session = updatedCaster };
        arguments[1] = updatedCaster;
        arguments[2] = result;
        arguments[3] = CancellationToken.None;
        await Assert.IsType<ValueTask>(send.Invoke(null, arguments));

        Assert.Contains("target NeptuneCurse player-hit damage block", log.ToString());
        Assert.DoesNotContain("attacker NeptuneCurse player-hit damage cap", log.ToString());
        var target = new HellVortexReceiveEnvelopeTarget(
            TargetPlayerUid, updatedTarget, targetSent, 840, 100_000, 193, 0, 0, MsgAttackEffect.None);
        AssertHellVortexReceiveEnvelopeShieldBeforeHitpoints(target, expectedShieldLeft: 0, expectFinalAnimation: false);
        Assert.Empty(ParseSpellAnimations(targetSent)); // The receive phase does not own final 2239 publication.
    }

    [Fact]
    public async Task HandlerHellVortexInitiallyNeptuneCursedTargetStillFailsGateWithoutReceivePublication()
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(targetStatusFlag: LegacyNeptuneCurseFlag);

        var target = Assert.Single(result.Targets);
        Assert.Equal(100_000, target.Session.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(target.Session.Character.Attributes, LegacyNeptuneCurseFlag));
        Assert.True(HasLegacyStatusFlag(target.Session.Character.Attributes, HellVortexReceiveEnvelopeHawksFlag));
        Assert.False(HasLegacyStatusFlag(target.Session.Character.Attributes, HellVortexReceiveEnvelopeSolidFlag));
        Assert.Empty(result.HookObservations);
        foreach (var frames in new[] { result.CasterSent, target.Sent })
        {
            var animation = Assert.Single(ParseSpellAnimations(frames));
            Assert.Empty(animation.Targets);
            var index = SingleFrameIndex(frames, frame => LegacySpellAnimationPacket.TryParse(frame, out _));
            Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
        }
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), update => update.Uid == target.Uid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or
                LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.AzureShield or LegacyMsgUpdateDataType.ArchiveSkill));
    }

    [Theory]
    [InlineData(0, 387U)]
    [InlineData(DefensiveStanceStatusFlag, 193U)]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag, 0U)]
    public async Task HandlerHellVortexAttackerNeptuneRewritesReceiveAnimationToTargetHitpointsMinusOne(
        int targetStatusFlag,
        uint expectedRequestedDamage)
    {
        var result = await RunHellVortexReceiveEnvelopeAsync(
            targetStatusFlag: targetStatusFlag,
            targetHitpoints: 900,
            targetSpirit: 0,
            attackerNeptuneCurse: true,
            expectedReceiveDamage: 899);

        var target = Assert.Single(result.Targets);
        Assert.Equal(expectedRequestedDamage, target.RequestedDamage);
        Assert.Equal(1, target.Session.Character.MinHitPoints);
        Assert.Contains("attacker NeptuneCurse player-hit damage cap", result.Log);
        foreach (var frames in new[] { result.CasterSent, target.Sent })
        {
            var hit = Assert.Single(Assert.Single(ParseSpellAnimations(frames)).Targets);
            Assert.Equal(899U, hit.Damage);
            Assert.True(hit.Hit);
            Assert.Equal(targetStatusFlag == DefensiveStanceStatusFlag ? MsgAttackEffect.Block : MsgAttackEffect.None, hit.Effect);
        }
        AssertHellVortexReceiveEnvelope(result);
    }

    private static void AssertHellVortexReceiveEnvelopeShieldBeforeHitpoints(
        HellVortexReceiveEnvelopeTarget target,
        uint expectedShieldLeft,
        bool expectFinalAnimation = true)
    {
        var shieldIndex = SingleFrameIndex(
            target.Sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == target.Uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield));
        Assert.Equal((ushort)2176, ReadFrame(target.Sent[shieldIndex]).Header.Opcode);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(target.Sent[shieldIndex]), out var shield));
        var entry = Assert.Single(shield.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.AzureShield, entry.Type);
        Assert.Equal(0UL, entry.Value);
        Assert.Equal((uint)LegacyLightningShieldActivatedFlag, entry.Flag);
        Assert.InRange(entry.Time, 0U, 10U);
        Assert.Equal(expectedShieldLeft, entry.Damage);
        Assert.Equal(3U, entry.SpellLevel);
        Assert.Equal(
            expectedShieldLeft.ToString(CultureInfo.InvariantCulture),
            target.Session.Character.Attributes["LightningShieldLeft"]);
        Assert.Equal(
            target.Session.Character.MinHitPoints > 0,
            HasLegacyStatusFlag(target.Session.Character.Attributes, LegacyLightningShieldActivatedFlag));

        var hitpointIndices = AssertHellVortexReceiveEnvelopeHitpoints(target, expectFinalAnimation);
        Assert.True(shieldIndex < hitpointIndices[0]);
    }
}
