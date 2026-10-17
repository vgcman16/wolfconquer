using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ArrowPrivateStarFlowFlag = 337;

    [Theory]
    [InlineData("flag-vector", false)]
    [InlineData("flag-vector", true)]
    [InlineData("owner-counter", false)]
    [InlineData("owner-counter", true)]
    public async Task ArrowBladesPrivateCounterOriginalOwnerLossStopsReceiveWithoutFallback(string phase, bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? ownerAtBoundary = null;
        WorldSession? replacement = null;
        await using var fixture = new ArrowLifecycleFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowPrivateCounterBoundary(bytes, phase))
            {
                armed = false;
                callbacks++;
                ownerAtBoundary = current.Owner;
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
                boundary = current.Order.Count; // Trigger frame already delivered; later generic vector peers remain allowed.
            }
            return ValueTask.CompletedTask;
        });
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AttackAsync(); // FIRST real ordinary input selects17400 and enters its player receive opening.
        Assert.Equal(1, callbacks);
        Assert.True(boundary > start);
        Assert.NotNull(ownerAtBoundary);
        Assert.Equal(5_000, ownerAtBoundary.Character.MinHitPoints);
        Assert.Equal(1_000, ownerAtBoundary.Character.MinMana);
        Assert.Equal(1U, ArrowLifeCounter(ownerAtBoundary, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(ownerAtBoundary, "StarFlow"));
        Assert.True(HasLegacyStatusFlag(ownerAtBoundary.Character.Attributes, ArrowPrivateStarFlowFlag));
        Assert.False(HasLegacyStatusFlag(ownerAtBoundary.Character.Attributes, ArrowLifeWater));
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal(77_777, fixture.OwnerSlot!.Character.MinHitPoints);
            Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            Assert.Equal("replacement", fixture.OwnerSlot.Character.Attributes["ArrowIdentity"]);
            Assert.Equal(0U, ArrowLifeCounter(fixture.OwnerSlot, "ArrowBlades"));
            Assert.Equal(0U, ArrowLifeCounter(fixture.OwnerSlot, "StarFlow"));
            Assert.False(HasLegacyStatusFlag(fixture.OwnerSlot.Character.Attributes, ArrowPrivateStarFlowFlag));
        }
        else Assert.Null(fixture.OwnerSlot);
        var ownerCounters = ArrowLifeTyped(fixture.FramesSince(PlayerUid, start), 136, ArrowPrivateStarFlowFlag);
        if (phase == "flag-vector") Assert.Empty(ownerCounters); // Owner was already lost before the private send.
        else ArrowLifeAssertIcon(Assert.Single(ownerCounters), 1); // Already-delivered original-owner136 remains legitimate.
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(TargetPlayerUid, start), 136, ArrowPrivateStarFlowFlag));
        foreach (var recipient in new[] { PlayerUid, TargetPlayerUid })
            ArrowPrivateAssertNoReceiveOrFallback(fixture.FramesSince(recipient, boundary));
    }

    [Theory]
    [InlineData("flag-vector", false)]
    [InlineData("flag-vector", true)]
    [InlineData("owner-counter", false)]
    [InlineData("owner-counter", true)]
    public async Task ArrowBladesPrivateCounterTargetLossPreservesOriginalOwnerPrefixAndPrivateCounter(string phase, bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? targetAtBoundary = null;
        WorldSession? replacement = null;
        await using var fixture = new ArrowLifecycleFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowPrivateCounterBoundary(bytes, phase))
            {
                armed = false;
                callbacks++;
                targetAtBoundary = current.Peer;
                if (rebind) replacement = current.RebindPeer();
                else current.RemovePeer();
                boundary = current.Order.Count;
            }
            return ValueTask.CompletedTask;
        });
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AttackAsync();
        Assert.Equal(1, callbacks);
        Assert.True(boundary > start);
        Assert.NotNull(targetAtBoundary);
        Assert.Equal(100_000, targetAtBoundary.Character.MinHitPoints);
        Assert.Equal((long)PlayerUid, fixture.Owner.Character.Uid);
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowPrivateStarFlowFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, ArrowLifeWater));
        Assert.False(LegacyArrowBladesFlagRegistration.TryRead(fixture.Owner.Character, ArrowLifeWater, out _));
        if (rebind)
        {
            Assert.Same(replacement, fixture.PeerSlot);
            Assert.Equal((long)(TargetPlayerUid + 9_000), fixture.PeerSlot!.Character.Uid);
            Assert.Equal(88_888, fixture.PeerSlot.Character.MinHitPoints);
            Assert.Equal(88, fixture.PeerSlot.Character.MinMana);
            Assert.Equal("target-replacement", fixture.PeerSlot.Character.Attributes["ArrowIdentity"]);
            Assert.Equal(0U, ArrowLifeCounter(fixture.PeerSlot, "ArrowBlades"));
            Assert.Equal(0U, ArrowLifeCounter(fixture.PeerSlot, "StarFlow"));
            Assert.False(HasLegacyStatusFlag(fixture.PeerSlot.Character.Attributes, ArrowPrivateStarFlowFlag));
        }
        else Assert.Null(fixture.PeerSlot);
        // Target-only loss does not cancel the entered owner's source-private136; then typed TargetLost skips receive.
        ArrowLifeAssertIcon(Assert.Single(ArrowLifeTyped(
            fixture.FramesSince(PlayerUid, start), 136, ArrowPrivateStarFlowFlag)), 1);
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(TargetPlayerUid, start), 136, ArrowPrivateStarFlowFlag));
        foreach (var recipient in new[] { PlayerUid, TargetPlayerUid })
            ArrowPrivateAssertNoReceiveOrFallback(fixture.FramesSince(recipient, boundary));
    }

    private static bool ArrowPrivateCounterBoundary(byte[] bytes, string phase)
    {
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) || packet.Uid != PlayerUid)
            return false;
        return phase == "flag-vector"
            ? packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is { } values && IsLegacyStatusFlagSet(values, ArrowPrivateStarFlowFlag))
            : packet.Updates.Any(entry => (uint)entry.Type == 136 && entry.Flag == ArrowPrivateStarFlowFlag);
    }

    private static void ArrowPrivateAssertNoReceiveOrFallback(byte[][] frames)
    {
        Assert.DoesNotContain(ParseMsgUpdates(frames), packet => packet.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        Assert.Empty(ParseSpellAnimations(frames)); // No stale17400 hit,18220/10950 proc, or retargeted replacement animation.
        Assert.DoesNotContain(frames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Query.AttackType is MsgAttackType.Physical or MsgAttackType.Archer or MsgAttackType.Death);
    }
}
