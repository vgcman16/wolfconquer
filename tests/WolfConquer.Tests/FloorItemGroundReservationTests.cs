using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// A managed save-before-publish adaptation, not proof of native Map ownership.
public sealed class FloorItemGroundReservationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    [Fact]
    public void PendingGroundIsInvisibleToEveryVisiblePickupExpiryAndEffectConsumer()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1) with
        {
            Effect = new FloorItemRuntimeEffect(100, 100, 100, Now.AddSeconds(-2),
                ExpiresAt: Now.AddSeconds(1), RepeatMilliseconds: 1, NextRepeatAt: Now.AddSeconds(-1)),
        };
        using var pending = Reserve(store, entry);

        Assert.Empty(store.SnapshotVisible(Now));
        Assert.False(store.TryGetVisiblePacket(1, Now, out _));
        Assert.False(store.TryGetVisibleEntry(1, Now, out _));
        Assert.False(store.TryConsumePickup(1, out _));
        Assert.False(store.TryConsumePickupEntry(1, out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Missing, store.GetPickupStatus(1, 77, Now, out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Missing, store.TryConsumePickupEntry(1, 77, true, Now, out _));
        Assert.False(store.TryRemoveVisible(1, out _));
        Assert.Empty(store.ConsumeExpired(Now.AddDays(1)));
        Assert.Empty(store.ConsumeDueEffects(100, Now));
        Assert.Empty(store.ConsumeDueRepeatEffects(100, Now));
        var effects = store.ConsumeDueRepeatingEffects(100, Now.AddSeconds(2));
        Assert.Empty(effects.Invoked);
        Assert.Empty(effects.Expired);
        Assert.True(pending.TryPublish(Now.AddDays(1))); // Consumers cannot retire a pending token.
    }

    [Fact]
    public void GroundSearchAvoidsReservedTileAndRetainsOrdinaryScatterOrder()
    {
        var store = new FloorItemRuntimeStateStore();
        using var first = Reserve(store, Entry(1));
        Assert.True(store.TryAddVisibleOnGround(Entry(2), Now, 0, null, out var added));
        Assert.Equal((99, 99), ((int)added.Packet.X, (int)added.Packet.Y));
        using var second = Reserve(store, Entry(3));
        Assert.Equal((100, 99), ((int)second.Entry.Packet.X, (int)second.Entry.Packet.Y));
        using var otherMap = Reserve(store, Entry(4, map: 1015));
        Assert.Equal((100, 100), ((int)otherMap.Entry.Packet.X, (int)otherMap.Entry.Packet.Y));
        Assert.Single(store.SnapshotVisible(Now));
        Assert.True(first.TryPublish(Now));
        Assert.True(second.TryPublish(Now));
        Assert.True(otherMap.TryPublish(Now));
    }

    [Fact]
    public void EveryPublicAddPathRejectsReservedUidOrTileWithoutPublishingAnything()
    {
        var store = new FloorItemRuntimeStateStore();
        using var pending = Reserve(store, Entry(1));
        var sameUid = Entry(1, 200, 200);
        Assert.Throws<InvalidOperationException>(() => store.AddVisiblePacket(sameUid.Packet));
        Assert.Throws<InvalidOperationException>(() => store.AddVisible(sameUid));
        Assert.Throws<InvalidOperationException>(() => store.AddVisible(Entry(2), Now));
        Assert.Throws<InvalidOperationException>(() => store.AddVisible(Entry(3) with
        { Packet = Entry(3).Packet with { DropType = LegacyFloorItemDropType.Effect } }, Now));
        Assert.False(store.TryAddVisibleOnGround(sameUid, 0, out _));
        Assert.False(store.TryAddVisibleOnGround(sameUid, Now, 0, null, out _));
        Assert.False(store.TryAddVisibleOnGround(Entry(2), Now, 0,
            (_, x, y) => x == 100 && y == 100, out _));
        Assert.Empty(store.SnapshotVisible(Now));
        Assert.True(pending.TryPublish(Now));
        Assert.Equal(pending.Entry, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Fact]
    public void ReservationRejectsVisibleAndPendingUidEvenAtDifferentLocations()
    {
        var store = new FloorItemRuntimeStateStore();
        store.AddVisible(Entry(1), Now);
        Assert.False(store.TryReserveGround(Entry(1, 200, 200), Now, 0, null, out var visibleCollision));
        Assert.Null(visibleCollision);
        using var pending = Reserve(store, Entry(2, 200, 200));
        Assert.False(store.TryReserveGround(Entry(2, 300, 300), Now, 0, null, out var pendingCollision));
        Assert.Null(pendingCollision);
        Assert.Equal(1U, Assert.Single(store.SnapshotVisible(Now)).Packet.Uid);
        Assert.True(pending.TryPublish(Now));
    }

    [Fact]
    public void DisposeReleasesUidTileAndCapacityWithoutLettingAnOldTokenPublishItsReplacement()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var abandoned = Reserve(store, Entry(1));
        Assert.False(store.TryReserveGround(Entry(2, 200, 200), Now, 0, null, out _));
        abandoned.Dispose();
        abandoned.Dispose();
        using var replacement = Reserve(store, Entry(1));
        Assert.Equal((100, 100), ((int)replacement.Entry.Packet.X, (int)replacement.Entry.Packet.Y));
        Assert.False(abandoned.TryPublish(Now));
        Assert.Empty(store.SnapshotVisible(Now));
        Assert.True(replacement.TryPublish(Now));
        Assert.Equal(replacement.Entry, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Fact]
    public void SuccessfulPublicationIsOneShotAndDisposeCannotRemovePublishedLoot()
    {
        var store = new FloorItemRuntimeStateStore();
        var pending = Reserve(store, Entry(1));
        Assert.True(pending.TryPublish(Now));
        Assert.False(pending.TryPublish(Now.AddSeconds(10)));
        pending.Dispose();
        Assert.False(pending.TryPublish(Now.AddSeconds(20)));
        Assert.Equal(pending.Entry, Assert.Single(store.SnapshotVisible(Now)));
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            store.TryConsumePickupEntry(1, 77, Now, out var consumed));
        Assert.Equal(pending.Entry, consumed);
        Assert.False(store.TryGetVisibleEntry(1, Now, out _));
    }

    [Fact]
    public void LifetimeAndOwnerWindowStartAtPublicationNotReservation()
    {
        var store = new FloorItemRuntimeStateStore();
        using var pending = Reserve(store, Entry(1) with { OwnerOnly = true });
        var committed = Now.AddHours(1);
        Assert.Empty(store.ConsumeExpired(committed));
        Assert.True(pending.TryPublish(committed));
        Assert.Equal(FloorItemRuntimePickupStatus.OwnerLocked,
            store.GetPickupStatus(1, 88, committed.AddSeconds(20), out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            store.GetPickupStatus(1, 88, committed.AddSeconds(21), out _));
        Assert.True(store.TryGetVisibleEntry(1, committed.AddSeconds(39), out _));
        Assert.Equal(pending.Entry, Assert.Single(store.ConsumeExpired(committed.AddSeconds(40))));
        Assert.False(store.TryGetVisibleEntry(1, committed.AddSeconds(40), out _));
    }

    [Fact]
    public void FullVisibleCapacityIsUntouchedByReservationOrFailedSaveDisposal()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var visible = Entry(1);
        store.AddVisible(visible, Now);
        using (var failedSave = Reserve(store, Entry(2, 200, 200)))
            Assert.Equal(visible, Assert.Single(store.SnapshotVisible(Now)));
        Assert.Equal(visible, Assert.Single(store.SnapshotVisible(Now)));
        using var retry = Reserve(store, Entry(2, 200, 200));
        Assert.True(retry.TryPublish(Now));
        Assert.Equal(retry.Entry, Assert.Single(store.SnapshotVisible(Now)));
        Assert.False(store.TryGetVisibleEntry(1, Now, out _));
    }

    [Fact]
    public void BoundedPendingTokensSurvivePublicEvictionAndPublishInNormalOldestOrder()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 2);
        store.AddVisible(Entry(1, 10, 10), Now);
        store.AddVisible(Entry(2, 20, 20), Now);
        using var first = Reserve(store, Entry(3, 30, 30));
        using var second = Reserve(store, Entry(4, 40, 40));
        Assert.False(store.TryReserveGround(Entry(5, 50, 50), Now, 0, null, out _));
        Assert.Equal(new uint[] { 1, 2 }, VisibleUids(store));
        store.AddVisible(Entry(5, 50, 50), Now);
        Assert.Equal(new uint[] { 2, 5 }, VisibleUids(store));
        Assert.True(first.TryPublish(Now));
        Assert.Equal(new uint[] { 3, 5 }, VisibleUids(store));
        Assert.Throws<InvalidOperationException>(() => store.AddVisible(Entry(4, 400, 400), Now));
        Assert.True(second.TryPublish(Now));
        Assert.Equal(new uint[] { 3, 4 }, VisibleUids(store));
        Assert.False(first.TryPublish(Now));
        Assert.False(second.TryPublish(Now));
    }

    [Fact]
    public void InvalidTerrainDoesNotEvictVisibleLootOrConsumePendingCapacity()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        store.AddVisible(Entry(1), Now);
        Assert.False(store.TryReserveGround(Entry(2), Now, 0, (_, _, _) => false, out var rejected));
        Assert.Null(rejected);
        Assert.Equal(1U, Assert.Single(store.SnapshotVisible(Now)).Packet.Uid);
        using var accepted = Reserve(store, Entry(2, 200, 200));
        Assert.Equal(1U, Assert.Single(store.SnapshotVisible(Now)).Packet.Uid);
    }

    [Fact]
    public void OrdinaryPublicDuplicateUpdateEffectStorageAndOldestEvictionStayCompatible()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 2);
        var now = DateTimeOffset.UtcNow;
        store.AddVisiblePacket(Entry(1).Packet);
        var updated = Entry(1, 101, 101);
        store.AddVisible(updated, now);
        Assert.Equal(updated, Assert.Single(store.SnapshotVisible(now)));
        var effect = Entry(2, 101, 101) with
        { Packet = Entry(2, 101, 101).Packet with { DropType = LegacyFloorItemDropType.Effect } };
        store.AddVisible(effect, now); // Regular adds retain their pre-existing visible-tile semantics.
        Assert.True(store.TryAddVisibleOnGround(Entry(3, 101, 101), now, 0, null, out var ground));
        Assert.NotEqual((101, 101), ((int)ground.Packet.X, (int)ground.Packet.Y));
        Assert.False(store.TryGetVisibleEntry(1, now, out _));
        Assert.True(store.TryGetVisibleEntry(2, now, out var storedEffect));
        Assert.Equal(effect, storedEffect);
        Assert.Equal(FloorItemRuntimePickupStatus.Available, store.TryConsumePickupEntry(3, 77, now, out _));
    }

    [Theory]
    [InlineData(LegacyFloorItemDropType.Effect)]
    [InlineData(LegacyFloorItemDropType.Pickup)]
    public void ReservationRetainsGroundAddInputValidation(LegacyFloorItemDropType type)
    {
        var store = new FloorItemRuntimeStateStore();
        Assert.Throws<ArgumentNullException>(() => store.TryReserveGround(null!, Now, 0, null, out _));
        var invalid = Entry(1) with { Packet = Entry(1).Packet with { DropType = type } };
        Assert.Throws<ArgumentException>(() => store.TryReserveGround(invalid, Now, 0, null, out _));
        using var accepted = Reserve(store, Entry(1));
        Assert.True(accepted.TryPublish(Now));
    }

    private static FloorItemRuntimeStateStore.GroundReservation Reserve(FloorItemRuntimeStateStore store, FloorItemRuntimeEntry entry)
    {
        Assert.True(store.TryReserveGround(entry, Now, 0, null, out var pending));
        return Assert.IsType<FloorItemRuntimeStateStore.GroundReservation>(pending);
    }

    private static uint[] VisibleUids(FloorItemRuntimeStateStore store) =>
        store.SnapshotVisible(Now).Select(entry => entry.Packet.Uid).Order().ToArray();

    private static FloorItemRuntimeEntry Entry(uint uid, ushort x = 100, ushort y = 100, long map = 1002) =>
        FloorItemRuntimeEntry.ForItem(map,
            new LegacyFloorItemPacket(uid, 50_000, x, y, 40, LegacyFloorItemDropType.Visible,
                0, 2, 77, 0, 0, 0, 0, 0, ""),
            new PlayerItemSnapshotItem { Uid = uid, ItemId = 50_000, StackSize = 3, Plus = 2 });
}
