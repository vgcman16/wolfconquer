using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task ArrowAndFlashingExpiryKeepsOneClientNowAcrossEnteredArrowVectorClockAdvance()
    {
        var armed = false;
        var callbacks = 0;
        await using var fixture = Flash0LifetimeFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && SharedExpiryVector(bytes, flashing: true, triggered: false))
            {
                armed = false;
                callbacks++;
                Assert.Equal(ArrowLifeOrigin.AddSeconds(12), current.Now);
                Assert.True(HasLegacyStatusFlag(current.Owner.Character.Attributes, 0));
                Assert.False(HasLegacyStatusFlag(current.Owner.Character.Attributes, 336));
                current.AdvanceArrowClockDuringEnteredRuntimeSend(TimeSpan.FromSeconds(20));
                Assert.Equal(ArrowLifeOrigin.AddSeconds(32), current.Now);
            }
            return ValueTask.CompletedTask;
        });
        var flash = await Flash0AcquireAsync(fixture); // Real0 and336 at W2; deadlines W32 andW12.
        fixture.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(2).DateTime, 10);
        var hitpoints = fixture.Peer.Character.MinHitPoints;
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(9_980)); // W11.980, both still active.
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20)); // Owner invocation captures W12 ONCE.
        Assert.Equal(1, callbacks);
        Assert.Equal(ArrowLifeOrigin.AddSeconds(32), fixture.Now);
        Assert.Same(flash, Flash0Read(fixture.Owner)); // Clock changed; captured W12 did not.
        ArrowLifeAssertRetired(fixture.Owner, 336);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        Assert.Equal(hitpoints, fixture.Peer.Character.MinHitPoints);
        var owner = fixture.FramesSince(PlayerUid, start);
        var peer = fixture.FramesSince(TargetPlayerUid, start);
        Assert.True(SharedExpiryVector(Assert.Single(owner), flashing: true, triggered: false));
        Assert.Equal(Assert.Single(owner), Assert.Single(peer));

        start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20)); // NEXT captured invocation W32.020.
        Flash0AssertRetired(fixture.Owner);
        Assert.True(SharedExpiryVector(Assert.Single(fixture.FramesSince(PlayerUid, start)), false, false));
        Assert.Equal(Assert.Single(fixture.FramesSince(PlayerUid, start)),
            Assert.Single(fixture.FramesSince(TargetPlayerUid, start)));
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Fact]
    public async Task ArrowAndFlashingSimultaneousExpiryPublishesTwoCapturedVectorsWithoutInventingFlagPriority()
    {
        await using var fixture = Flash0LifetimeFixture();
        var flash = await Flash0AcquireAsync(fixture);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(18)); // W20: first336 expired, old0 still active.
        await fixture.OrdinaryMagicAsync(); // W20 Star1.
        await fixture.OrdinaryMagicAsync(); // W21 Star2.
        await fixture.OrdinaryMagicAsync(); // W22: real replacement336, duplicate0 remains quiet.
        Assert.Equal(ArrowLifeOrigin.AddSeconds(22), fixture.Now);
        Assert.Same(flash, Flash0Read(fixture.Owner));
        var trigger = fixture.AssertRegistration(336, ArrowLifeOrigin.AddSeconds(22).DateTime, 10);
        Assert.Equal(flash.ExpiresAtLocal, trigger.ExpiresAtLocal); // Both actual deadlines W32.
        var hitpoints = fixture.Peer.Character.MinHitPoints;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(9_980));
        var start = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        Flash0AssertRetired(fixture.Owner);
        ArrowLifeAssertRetired(fixture.Owner, 336);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        Assert.Equal(hitpoints, fixture.Peer.Character.MinHitPoints);
        var owner = ArrowLifeVectors(fixture.FramesSince(PlayerUid, start));
        var peer = ArrowLifeVectors(fixture.FramesSince(TargetPlayerUid, start));
        Assert.Equal(2, owner.Length);
        Assert.Equal(2, peer.Length);
        Assert.True(SharedExpiryVector(owner[0], false, true) || SharedExpiryVector(owner[0], true, false));
        Assert.True(SharedExpiryVector(owner[1], false, false));
        Assert.Equal(owner[0], peer[0]);
        Assert.Equal(owner[1], peer[1]);
        Assert.Collection(fixture.Order.ToArray().Skip(start),
            frame => { Assert.Equal(PlayerUid, frame.Recipient); Assert.Equal(owner[0], frame.Bytes); },
            frame => { Assert.Equal(TargetPlayerUid, frame.Recipient); Assert.Equal(owner[0], frame.Bytes); },
            frame => { Assert.Equal(PlayerUid, frame.Recipient); Assert.Equal(owner[1], frame.Bytes); },
            frame => { Assert.Equal(TargetPlayerUid, frame.Recipient); Assert.Equal(owner[1], frame.Bytes); });
        Assert.Equal(3U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArrowAndFlashingSharedCaptureStopsOriginalOwnerAfterEnteredExpiryRemoval(bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? replacement = null;
        await using var fixture = Flash0LifetimeFixture((current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && SharedExpiryVector(bytes, true, false))
            {
                armed = false;
                callbacks++;
                current.AdvanceArrowClockDuringEnteredRuntimeSend(TimeSpan.FromSeconds(20));
                if (rebind) replacement = current.RebindOwner(); else current.RemoveOwner();
                boundary = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        await Flash0AcquireAsync(fixture);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(9_980));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(20));
        Assert.Equal(1, callbacks);
        Assert.True(boundary > start);
        Assert.True(SharedExpiryVector(Assert.Single(fixture.FramesSince(PlayerUid, start)), true, false));
        Assert.Empty(fixture.FramesSince(TargetPlayerUid, start));
        Assert.Empty(fixture.Order.ToArray().Skip(boundary));
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
            Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            Assert.Null(fixture.OwnerSlot.CanAttackFlashingRegistration);
        }
        else Assert.Null(fixture.OwnerSlot);
    }

    private static bool SharedExpiryVector(byte[] bytes, bool flashing, bool triggered) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && IsLegacyStatusFlagSet(values, 0) == flashing &&
            IsLegacyStatusFlagSet(values, 336) == triggered &&
            !IsLegacyStatusFlagSet(values, 337) && IsLegacyStatusFlagSet(values, 333));
}
