using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ArrowThirdCharge = 337;
    private const int ArrowThirdTriggered = 336;

    [Fact]
    public async Task ArrowBladesThirdStarFlowSeparatesBothVectorsBeforeUnavailableSpellAndUsesFreshAddTime()
    {
        var armed = false;
        WorldSession? removed = null;
        WorldSession? added = null;
        await using var fixture = new ArrowLifecycleFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, charge: false, triggered: false))
            {
                removed = current.Owner;
                current.AdvanceUnstartedArrowInputClock(TimeSpan.FromSeconds(2));
            }
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, charge: false, triggered: true))
            {
                added = current.Owner;
                current.AdvanceUnstartedArrowInputClock(TimeSpan.FromSeconds(1));
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync(); // Third actual receive enters at W2; no learned10950.
        Assert.NotNull(removed);
        Assert.NotNull(added);
        Assert.Equal(4U, ArrowLifeCounter(removed, "StarFlow"));
        Assert.Equal(4U, ArrowLifeCounter(added, "StarFlow"));
        Assert.Equal(3U, ArrowLifeCounter(removed, "ArrowBlades"));
        ArrowLifeAssertRetired(removed, ArrowThirdCharge);
        ArrowLifeAssertRetired(removed, ArrowThirdTriggered);
        ArrowLifeAssertRetired(added, ArrowThirdCharge);
        Assert.True(HasLegacyStatusFlag(added.Character.Attributes, ArrowThirdTriggered));
        fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(4).DateTime, 10);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(5), fixture.Now);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.False(fixture.Owner.PlayerSpells.ContainsSpell(10_950));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, start);
            ArrowThirdAssertCompletePrefix(frames);
            Assert.DoesNotContain(ParseSpellAnimations(frames), packet => packet.SpellId == 10_950);
            Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == 1_000);
        }
        var ownerVectors = ArrowLifeVectors(fixture.FramesSince(PlayerUid, start));
        var peerVectors = ArrowLifeVectors(fixture.FramesSince(TargetPlayerUid, start));
        Assert.Equal(ownerVectors.Length, peerVectors.Length);
        for (var index = 0; index < ownerVectors.Length; index++) Assert.Equal(ownerVectors[index], peerVectors[index]);
    }

    [Fact]
    public async Task ArrowBladesThirdStarFlowReadsCurrentOwnerCoordinatesAndVitalsAfterBothPrefixVectors()
    {
        var armed = false;
        var peerHitpointsBeforeProc = 0L;
        await using var fixture = new ArrowLifecycleFixture(learnedStarFlow: true, onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, charge: false, triggered: false))
                current.ChangeOwnerDuringArrowSend(value => value with
                { Character = value.Character with { X = 831, Y = 584 } });
            if (armed && recipient == PlayerUid && ArrowThirdVector(bytes, charge: false, triggered: true))
            {
                peerHitpointsBeforeProc = current.Peer.Character.MinHitPoints;
                current.ChangeOwnerDuringArrowSend(value => value with
                { Character = value.Character with { X = 833, Y = 585, MinHitPoints = 4_444, MinMana = 77 } });
                current.ChangePeerDuringArrowSend(value => value with
                { Character = value.Character with { X = 835, Y = 585 } });
            }
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(833, fixture.Owner.Character.X);
        Assert.Equal(585, fixture.Owner.Character.Y);
        Assert.Equal(4_444, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(77, fixture.Owner.Character.MinMana);
        Assert.Equal(835, fixture.Peer.Character.X);
        Assert.Equal(585, fixture.Peer.Character.Y);
        Assert.True(peerHitpointsBeforeProc > 0 && fixture.Peer.Character.MinHitPoints < peerHitpointsBeforeProc);
        Assert.Equal(5L, Math.Max(Math.Abs(fixture.Peer.Character.X - 830), Math.Abs(fixture.Peer.Character.Y - 584)));
        Assert.Equal(4L, Math.Max(Math.Abs(fixture.Peer.Character.X - 831), Math.Abs(fixture.Peer.Character.Y - 584)));
        Assert.Equal(2L, Math.Max(Math.Abs(fixture.Peer.Character.X - fixture.Owner.Character.X), Math.Abs(fixture.Peer.Character.Y - fixture.Owner.Character.Y)));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(uid, start);
            ArrowThirdAssertCompletePrefix(frames);
            var animation = Assert.Single(ParseSpellAnimations(frames), packet => packet.SpellId == 10_950);
            Assert.Equal((ushort)833, animation.X);
            Assert.Equal((ushort)585, animation.Y);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.True(Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid).Damage > 0);
            var all = frames.ToArray();
            var addIndex = Array.FindIndex(all, bytes => ArrowThirdVector(bytes, charge: false, triggered: true));
            var spellIndex = Array.FindIndex(all, bytes => ParseSpellAnimations([bytes]).Any(packet => packet.SpellId == 10_950));
            Assert.True(addIndex >= 0 && spellIndex > addIndex);
        }
    }

    [Fact]
    public async Task ArrowBladesThirdStarFlowDoesNotOverwriteCurrentChargeCreatedByNestedRealInputs()
    {
        var armed = false;
        var nested = 0;
        await using var fixture = new ArrowLifecycleFixture(onSend: async (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !ArrowThirdVector(bytes, charge: false, triggered: true)) return;
            armed = false;
            nested++;
            await current.OrdinaryMagicAsync(); // CurrentStar4→0 through an actual entered receive.
            await current.OrdinaryMagicAsync(); // Next actual receive creates Star1/337 at W4.
        });
        await ArrowThirdWarmAsync(fixture);
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, nested);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(4), fixture.Now);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        fixture.AssertRegistration(ArrowThirdCharge, ArrowLifeOrigin.AddSeconds(4).DateTime, 2_592_000);
        fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        Assert.False(fixture.Owner.PlayerSpells.ContainsSpell(10_950));
    }

    [Fact]
    public async Task ArrowBladesThirdStarFlow336ExpiresAtInclusiveTenSecondsWithoutResettingCountersOr333()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await ArrowThirdWarmAsync(fixture);
        await fixture.OrdinaryMagicAsync();
        var registration = fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        var water = fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000);
        Assert.Equal("true", fixture.Owner.Character.Attributes[LegacyStatusFlagDeathRegistration.Key(ArrowThirdTriggered)]);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.False(fixture.Owner.PlayerSpells.ContainsSpell(10_950));
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(9_980));
        Assert.Equal(registration, fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(2).DateTime, 10));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        ArrowLifeAssertRetired(fixture.Owner, ArrowThirdTriggered);
        Assert.Equal(water, fixture.AssertRegistration(ArrowLifeWater, ArrowLifeOrigin.AddSeconds(2).DateTime, 2_592_000));
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var bytes = Assert.Single(fixture.FramesSince(uid, start));
            Assert.True(ArrowThirdVector(bytes, charge: false, triggered: false));
        }
    }

    [Fact]
    public async Task ArrowBladesTwoNaturalThirdStepsQuietlyRenew336AndIgnoreItsOldDeadline()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await ArrowThirdWarmAsync(fixture);
        await fixture.OrdinaryMagicAsync();
        var first = fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        await fixture.OrdinaryMagicAsync();
        await fixture.OrdinaryMagicAsync();
        var start = fixture.Order.Count;
        await fixture.OrdinaryMagicAsync();
        var second = fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(5).DateTime, 10);
        Assert.NotEqual(first.Identity, second.Identity);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var vectors = ArrowLifeVectors(fixture.FramesSince(uid, start));
            Assert.Equal(2, vectors.Length);
            Assert.All(vectors, bytes => Assert.True(ArrowThirdVector(bytes, charge: false, triggered: true)));
        }
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(7)); // Old W12deadline.
        Assert.Equal(second, fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(5).DateTime, 10));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(2_980));
        Assert.Equal(second, fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(5).DateTime, 10));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        ArrowLifeAssertRetired(fixture.Owner, ArrowThirdTriggered);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task ArrowBladesNormalDeathRetiresActuallyEarned336WithoutResettingEitherCounter()
    {
        await using var fixture = new ArrowLifecycleFixture();
        await ArrowThirdWarmAsync(fixture);
        await fixture.OrdinaryMagicAsync();
        fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        await fixture.KillOwnerAsync();
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        ArrowLifeAssertRetired(fixture.Owner, ArrowThirdTriggered);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ArrowBladesThirdStepOriginalOwnerLossStopsAtItsEnteredPrefixVector(bool duringAdd, bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        WorldSession? replacement = null;
        WorldSession? entered = null;
        await using var fixture = new ArrowLifecycleFixture(onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !ArrowThirdVector(bytes, charge: false, triggered: duringAdd))
                return ValueTask.CompletedTask;
            armed = false;
            callbacks++;
            entered = current.Owner;
            if (rebind) replacement = current.RebindOwner();
            else current.RemoveOwner();
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var hp = fixture.Peer.Character.MinHitPoints;
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, callbacks);
        Assert.NotNull(entered);
        Assert.Equal(4U, ArrowLifeCounter(entered, "StarFlow"));
        Assert.Equal(duringAdd, HasLegacyStatusFlag(entered.Character.Attributes, ArrowThirdTriggered));
        Assert.Equal(hp, fixture.Peer.Character.MinHitPoints);
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
            Assert.Equal(0U, ArrowLifeCounter(fixture.OwnerSlot, "StarFlow"));
            Assert.False(HasLegacyStatusFlag(fixture.OwnerSlot.Character.Attributes, ArrowThirdTriggered));
        }
        else Assert.Null(fixture.OwnerSlot);
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        var ownerVectors = ArrowLifeVectors(owner);
        var peerVectors = ArrowLifeVectors(peer);
        if (duringAdd)
        {
            ArrowThirdAssertCompletePrefix(owner);
            Assert.Collection(peerVectors,
                bytes => Assert.True(ArrowThirdVector(bytes, charge: true, triggered: false)),
                bytes => Assert.True(ArrowThirdVector(bytes, charge: false, triggered: false)));
        }
        else
        {
            Assert.Collection(ownerVectors,
                bytes => Assert.True(ArrowThirdVector(bytes, charge: true, triggered: false)),
                bytes => Assert.True(ArrowThirdVector(bytes, charge: false, triggered: false)));
            Assert.Collection(peerVectors,
                bytes => Assert.True(ArrowThirdVector(bytes, charge: true, triggered: false)));
        }
        for (var index = 0; index < peerVectors.Length; index++) Assert.Equal(ownerVectors[index], peerVectors[index]);
        foreach (var frames in new[] { owner, peer }) ArrowThirdAssertNoVictimReceive(frames);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ArrowBladesThirdStepTargetLossStillOwesBothOwnerVectorsAndResetBeforeSkippingVictim(bool duringAdd, bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        WorldSession? replacement = null;
        WorldSession? oldTarget = null;
        await using var fixture = new ArrowLifecycleFixture(onSend: (current, recipient, bytes) =>
        {
            if (!armed || recipient != PlayerUid || !ArrowThirdVector(bytes, charge: false, triggered: duringAdd))
                return ValueTask.CompletedTask;
            armed = false;
            callbacks++;
            oldTarget = current.Peer;
            if (rebind) replacement = current.RebindPeer();
            else current.RemovePeer();
            return ValueTask.CompletedTask;
        });
        await ArrowThirdWarmAsync(fixture);
        var hp = fixture.Peer.Character.MinHitPoints;
        var start = fixture.Order.Count;
        armed = true;
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(1, callbacks);
        Assert.NotNull(oldTarget);
        Assert.Equal(hp, oldTarget.Character.MinHitPoints);
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        ArrowLifeAssertRetired(fixture.Owner, ArrowThirdCharge);
        fixture.AssertRegistration(ArrowThirdTriggered, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        Assert.Equal(2, ArrowLifeVectors(fixture.FramesSince(PlayerUid, start)).Count(bytes => ArrowThirdVector(bytes, false, false) || ArrowThirdVector(bytes, false, true)));
        if (rebind)
        {
            Assert.Same(replacement, fixture.PeerSlot);
            Assert.Equal(88_888, fixture.PeerSlot!.Character.MinHitPoints);
            Assert.Equal(88, fixture.PeerSlot.Character.MinMana);
            Assert.Equal(0U, ArrowLifeCounter(fixture.PeerSlot, "StarFlow"));
        }
        else Assert.Null(fixture.PeerSlot);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            ArrowThirdAssertNoVictimReceive(fixture.FramesSince(uid, start));
    }

    private static async Task ArrowThirdWarmAsync(ArrowLifecycleFixture fixture)
    {
        await fixture.AttackAsync();
        await fixture.OrdinaryMagicAsync();
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(2U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowThirdCharge));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowThirdTriggered));
    }

    private static bool ArrowThirdVector(byte[] bytes, bool charge, bool triggered) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } values &&
            IsLegacyStatusFlagSet(values, ArrowThirdCharge) == charge && IsLegacyStatusFlagSet(values, ArrowThirdTriggered) == triggered);

    private static void ArrowThirdAssertCompletePrefix(byte[][] frames) => Assert.Collection(ArrowLifeVectors(frames),
        bytes => Assert.True(ArrowThirdVector(bytes, charge: true, triggered: false)), // Source333add first.
        bytes => Assert.True(ArrowThirdVector(bytes, charge: false, triggered: false)), // SourceRemove337.
        bytes => Assert.True(ArrowThirdVector(bytes, charge: false, triggered: true))); // SourceAdd336.

    private static void ArrowThirdAssertNoVictimReceive(byte[][] frames)
    {
        Assert.DoesNotContain(ParseMsgUpdates(frames), packet => (packet.Uid == TargetPlayerUid || packet.Uid == TargetPlayerUid + 9_000) &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.DoesNotContain(ParseSpellAnimations(frames).SelectMany(packet => packet.Targets),
            target => target.Uid == TargetPlayerUid || target.Uid == TargetPlayerUid + 9_000);
        Assert.DoesNotContain(frames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Query.AttackType == MsgAttackType.Death);
    }
}
