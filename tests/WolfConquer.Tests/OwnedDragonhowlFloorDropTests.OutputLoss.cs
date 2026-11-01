using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFloorDropTests
{
    [Theory]
    [InlineData((ushort)17_490, true)]
    [InlineData((ushort)17_490, false)]
    [InlineData((ushort)17_650, true)]
    [InlineData((ushort)17_650, false)]
    public Task FirstActualFloorSenderLossRetainsPublishedEntryAndUidBeforeFencingAllLaterPhases(
        ushort spellId, bool money) => Run(spellId, money, walkable: true, async f =>
    {
        var files = ExpectedFiles(f, reward: false);
        uint beforeUid = FloorUid(), committedUid = unchecked(beforeUid + 1U);
        FloorItemRuntimeEntry? published = null;
        Exception? observationFailure = null;
        var senderFailure = new SocketException((int)SocketError.ConnectionReset);
        int attempts = 0;
        f.Owner.BeforeSend = (recipient, bytes) =>
        {
            if (!LegacyFloorItemPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet)) return;
            try
            {
                Assert.Equal(Actor, recipient); Assert.Equal(1, ++attempts);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.Equal(FloorGolden(committedUid, f.ItemId, 11, 10), bytes);
                Assert.Equal(committedUid, FloorUid());
                Assert.True(f.Floor.TryGetVisibleEntry(packet.Uid, f.Clock.GetUtcNow(), out published));
                Assert.Equal(f.ItemId, published.Packet.ItemId);
                Assert.Equal(money ? FloorItemRuntimePickupKind.Money : FloorItemRuntimePickupKind.Item, published.Kind);
                Assert.Equal(money ? 50L : 0L, published.Amount);
                Assert.Equal(77U, published.DynamicId); Assert.True(published.OwnerOnly);
                AssertAcceptedDeathPrefix(f);
                Assert.False(f.Binding.IsRetired); Assert.False(f.Owner.Recovery.IsBlocked);
                Assert.Equal(639_778_904L, f.Owner.Current().Character.ConquerPoints);
                Assert.DoesNotContain(f.Owner.ActorFrames, frame => Animation(frame, f.SpellId));
                Assert.DoesNotContain(f.Owner.ActorFrames, frame => IsCps(frame, 639_778_909));
                f.Owner.Data.AssertFiles(files);
            }
            catch (Exception error) { observationFailure = error; throw; }
            throw senderFailure;
        };
        // Expected real sender loss travels through the Frame. Do not use the
        // shared fixture's observation-error latch as the exception oracle.
        try { await f.RawAttack(); }
        finally { f.Owner.BeforeSend = null; }
        if (observationFailure is not null) ExceptionDispatchInfo.Capture(observationFailure).Throw();
        Assert.Equal(1, attempts);
        Assert.NotNull(published);
        AssertFenced(f);
        Assert.Equal(committedUid, FloorUid());
        Assert.Empty(FloorPackets(f)); // Failed transport did not record success.
        Assert.Same(published, Assert.Single(f.Floor.SnapshotVisible(f.Clock.GetUtcNow())));
        f.Owner.Data.AssertFiles(files);
        await AssertReplayFenced(f, files, committedUid);
        Assert.Same(published, Assert.Single(f.Floor.SnapshotVisible(f.Clock.GetUtcNow())));
    });
}
