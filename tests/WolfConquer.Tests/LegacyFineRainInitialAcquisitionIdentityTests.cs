using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("countdown", false)]
    [InlineData("countdown", true)]
    [InlineData("add212", false)]
    [InlineData("add212", true)]
    public async Task FineRainInitialAcquisitionStopsAtEachOwnerSendAfterOriginalUidLoss(string phase, bool replace)
    {
        var callbacks = 0;
        var callbackEnd = -1;
        WorldSession? replacement = null;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            var reached = phase == "countdown"
                ? FineRainInitialTryCountdown(bytes, PlayerUid, out var entry) && entry.Flag == 212
                : FineRainInitialTryVector(bytes, PlayerUid, out var first, out var second) && first && !second;
            if (recipient != PlayerUid || !reached || Interlocked.Exchange(ref callbacks, 1) != 0)
                return ValueTask.CompletedTask;
            if (replace) replacement = current.RebindInitialAcquisitionSubject(PlayerUid);
            else current.RemoveInitialAcquisitionSubject(PlayerUid);
            callbackEnd = current.Order.Count;
            return ValueTask.CompletedTask;
        }, power: 96);
        var start = fixture.Order.Count;
        await fixture.CastAsync();
        Assert.Equal(1, callbacks);
        Assert.True(callbackEnd > start);
        Assert.Empty(fixture.Order.ToArray().Skip(callbackEnd)); // No peer copy, next countdown/add, query, fill or animation.
        var cast = fixture.Order.ToArray().Skip(start).ToArray();
        var countdowns = cast.Where(frame => FineRainInitialTryCountdown(frame.Bytes, PlayerUid, out _)).ToArray();
        Assert.Equal(phase == "countdown" ? 1 : 4, countdowns.Length);
        if (phase == "countdown")
        {
            Assert.Equal(PlayerUid, countdowns[0].Recipient);
            Assert.DoesNotContain(cast, frame => FineRainInitialTryVector(frame.Bytes, PlayerUid, out _, out _));
        }
        else
        {
            FineRainInitialAssertVectors(fixture, start, [(PlayerUid, true, false)]);
        }
        Assert.Empty(ParseSpellAnimations(cast.Select(frame => frame.Bytes)));
        Assert.DoesNotContain(cast, frame => LegacyShowEquipmentPacket.TryParse(ReadFrame(frame.Bytes), out _));
        Assert.DoesNotContain(cast, frame => DirectQueryHpIsHpPacket(frame.Bytes));
        if (replace)
        {
            Assert.Same(replacement, fixture.InitialAcquisitionSlot(PlayerUid));
            FineRainInitialAssertReplacement(replacement!);
        }
        else Assert.Null(fixture.InitialAcquisitionSlot(PlayerUid));
    }

    [Theory]
    [InlineData("member", false)]
    [InlineData("member", true)]
    [InlineData("caster", false)]
    [InlineData("caster", true)]
    public async Task FineRainInitialAcquisitionSeparatesMemberLossFromRequiredCasterLossAtMemberAdd(
        string actor, bool replace)
    {
        var armed = false;
        var callbacks = 0;
        var callbackEnd = -1;
        WorldSession? replacement = null;
        WorldSession? memberAtLoss = null;
        await using var fixture = new FineRainExpiryFixture((current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid ||
                !FineRainInitialTryVector(bytes, TargetPlayerUid, out var first, out var second) || !first || second ||
                Interlocked.Exchange(ref callbacks, 1) != 0) return ValueTask.CompletedTask;
            var lost = actor == "member" ? TargetPlayerUid : PlayerUid;
            if (replace) replacement = current.RebindInitialAcquisitionSubject(lost);
            else current.RemoveInitialAcquisitionSubject(lost);
            memberAtLoss = current.InitialAcquisitionSlot(TargetPlayerUid);
            callbackEnd = current.Order.Count;
            return ValueTask.CompletedTask;
        }, power: 96, initialAcquisitionTeam: true);
        await fixture.JoinInitialAcquisitionTeamAsync(); // REAL MsgTeam Create + AcceptInvitation, not store-only membership.
        var start = fixture.Order.Count;
        armed = true;
        await fixture.CastAsync();
        armed = false;
        Assert.Equal(1, callbacks);
        Assert.True(callbackEnd > start);
        var resumed = fixture.Order.ToArray().Skip(callbackEnd).ToArray();
        var memberVectors = fixture.Order.ToArray().Skip(start)
            .Where(frame => FineRainInitialTryVector(frame.Bytes, TargetPlayerUid, out _, out _)).ToArray();
        var firstMemberVector = Assert.Single(memberVectors);
        Assert.Equal(TargetPlayerUid, firstMemberVector.Recipient); // Required identity lost before even this vector's caster peer copy.
        Assert.True(FineRainInitialTryVector(firstMemberVector.Bytes, TargetPlayerUid, out var member212, out var member213));
        Assert.True(member212);
        Assert.False(member213);
        Assert.DoesNotContain(resumed, frame => FineRainInitialTryCountdown(frame.Bytes, TargetPlayerUid, out _));
        Assert.DoesNotContain(resumed, frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame.Bytes), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        if (actor == "caster")
        {
            Assert.Empty(resumed); // Required ORIGINAL caster loss aborts the outer entered action, not only this member.
            Assert.Same(memberAtLoss, fixture.InitialAcquisitionSlot(TargetPlayerUid));
            Assert.NotNull(memberAtLoss);
            Assert.True(HasLegacyStatusFlag(memberAtLoss.Character.Attributes, 212));
            Assert.False(HasLegacyStatusFlag(memberAtLoss.Character.Attributes, 213));
            Assert.Equal(500, memberAtLoss.Character.MinHitPoints);
        }
        else
        {
            Assert.NotEmpty(resumed); // Lost member skipped; surviving caster still owns its query, fill and final animation.
            FineRainExpiryAssertOneQuery(fixture.OwnerFramesSince(callbackEnd));
            Assert.Equal(596, fixture.Owner.Character.MinHitPoints);
            DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 596);
            DirectQueryHpAssertPairs(fixture.OwnerFramesSince(callbackEnd),
                [(500U, 300U), (596U, 300U), (596U, 300U), (596U, 596U)]);
            var final = Assert.Single(ParseSpellAnimations(fixture.OwnerFramesSince(callbackEnd)),
                packet => packet.SpellId == 14_500);
            Assert.Equal(PlayerUid, Assert.Single(final.Targets).Uid); // No lost/replacement member is enqueued.
            foreach (var animation in ParseSpellAnimations(resumed.Select(frame => frame.Bytes)))
                Assert.Equal(PlayerUid, Assert.Single(animation.Targets).Uid);
            // A rebound connection may legitimately view the caster's final animation;
            // that presentation is not a member HP/query/result or session resurrection.
        }
        var slot = fixture.InitialAcquisitionSlot(actor == "member" ? TargetPlayerUid : PlayerUid);
        if (replace)
        {
            Assert.Same(replacement, slot);
            FineRainInitialAssertReplacement(replacement!);
        }
        else Assert.Null(slot);
    }

    private static void FineRainInitialAssertReplacement(WorldSession replacement)
    {
        Assert.Equal(77_777, replacement.Character.MinHitPoints);
        Assert.Equal(77, replacement.Character.MinMana);
        Assert.Equal("initial-replacement", replacement.Character.Attributes["FineRainExpiryIdentity"]);
        Assert.False(HasLegacyStatusFlag(replacement.Character.Attributes, 212));
        Assert.False(HasLegacyStatusFlag(replacement.Character.Attributes, 213));
        Assert.False(replacement.Character.Attributes.ContainsKey("FineRainPower"));
        Assert.False(replacement.Character.Attributes.ContainsKey("defFineRainPower"));
        Assert.False(replacement.Character.Attributes.ContainsKey("LegacyFineRain212Invocation.TimerTicks"));
        Assert.Null(replacement.EquipmentHitpointsStatus);
        Assert.Null(replacement.FineRainEquipmentAdmission);
    }
}
