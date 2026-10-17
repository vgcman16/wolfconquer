using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArrowBladesStarFlowVectorOriginalOwnerLossStopsLaterVisibleFanout(bool rebind)
    {
        var armed = false;
        var callbacks = 0;
        var boundary = -1;
        WorldSession? enteredOwner = null;
        WorldSession? replacement = null;
        await using var fixture = new ArrowLifecycleFixture(onSend: (current, recipient, bytes) =>
        {
            if (armed && recipient == PlayerUid && ArrowStarFlowOwnerVector(bytes))
            {
                armed = false;
                callbacks++;
                enteredOwner = current.Owner;
                if (rebind) replacement = current.RebindOwner();
                else current.RemoveOwner();
                boundary = current.Order.Count; // Owner's captured vector has been delivered; no337 peer send yet.
            }
            return ValueTask.CompletedTask;
        });
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "ArrowBlades"));
        Assert.Equal(0U, ArrowLifeCounter(fixture.Owner, "StarFlow"));
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        var start = fixture.Order.Count;
        armed = true;
        await fixture.AttackAsync(); // Real first17400 receive earns337; no counter, flag, timer or outcome seed.
        Assert.Equal(1, callbacks);
        Assert.True(boundary > start);
        Assert.Single(fixture.FramesSince(PlayerUid, start), ArrowStarFlowOwnerVector);
        Assert.DoesNotContain(fixture.FramesSince(TargetPlayerUid, boundary), ArrowStarFlowOwnerVector);
        Assert.NotNull(enteredOwner);
        Assert.Equal(1U, ArrowLifeCounter(enteredOwner, "ArrowBlades"));
        Assert.Equal(1U, ArrowLifeCounter(enteredOwner, "StarFlow"));
        Assert.True(HasLegacyStatusFlag(enteredOwner.Character.Attributes, 337));
        Assert.Equal(5_000, enteredOwner.Character.MinHitPoints);
        Assert.Equal(100_000, fixture.Peer.Character.MinHitPoints);
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(PlayerUid, boundary), 136, 337));
        Assert.Empty(ArrowLifeTyped(fixture.FramesSince(TargetPlayerUid, boundary), 136, 337));
        if (rebind)
        {
            Assert.Same(replacement, fixture.OwnerSlot);
            Assert.Equal((long)(PlayerUid + 9_000), fixture.OwnerSlot!.Character.Uid);
            Assert.Equal(77_777, fixture.OwnerSlot.Character.MinHitPoints);
            Assert.Equal(77, fixture.OwnerSlot.Character.MinMana);
            Assert.Equal("replacement", fixture.OwnerSlot.Character.Attributes["ArrowIdentity"]);
            Assert.Equal(0U, ArrowLifeCounter(fixture.OwnerSlot, "ArrowBlades"));
            Assert.Equal(0U, ArrowLifeCounter(fixture.OwnerSlot, "StarFlow"));
            Assert.False(HasLegacyStatusFlag(fixture.OwnerSlot.Character.Attributes, 337));
        }
        else Assert.Null(fixture.OwnerSlot);
        foreach (var recipient in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(recipient, boundary);
            Assert.DoesNotContain(ParseMsgUpdates(frames), packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
            Assert.Empty(ParseSpellAnimations(frames));
            Assert.DoesNotContain(frames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType is MsgAttackType.Physical or MsgAttackType.Archer or MsgAttackType.Death);
        }
    }

    private static bool ArrowStarFlowOwnerVector(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && IsLegacyStatusFlagSet(values, 337));
}
