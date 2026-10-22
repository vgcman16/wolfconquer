using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// In-process floor ownership only; no restart/power-loss ground durability claim.
public sealed class FloorItemPickupReservationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void ReservedPickupStaysVisibleButEveryOtherPickupConsumerIsBusy()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1); store.AddVisible(entry, Now);
        using var token = Reserve(store, entry);
        Assert.Same(entry, token.Entry);
        Assert.Same(entry, Assert.Single(store.SnapshotVisible(Now)));
        Assert.True(store.TryGetVisibleEntry(1, Now, out var visible)); Assert.Same(entry, visible);
        Assert.True(store.TryGetVisiblePacket(1, Now, out var packet)); Assert.Same(entry.Packet, packet);
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.GetPickupStatus(1, 77, Now, out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.TryConsumePickupEntry(1, 77, Now, out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.TryConsumePickupEntry(1, 88, true, Now, out _));
        Assert.False(store.TryConsumePickupEntry(1, out _));
        Assert.False(store.TryConsumePickup(1, out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.TryReservePickup(entry, 77, false, Now, out var rival));
        Assert.Null(rival);
        Assert.Same(entry, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Fact]
    public void FailedSaveDisposalReleasesWithoutConsumingAndOldTokenCannotStealRetry()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1); store.AddVisible(entry, Now);
        var original = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([entry.Item!], [], [], [], []));
        var failedSave = Reserve(store, entry);
        failedSave.Dispose(); failedSave.Dispose();
        Assert.Equal(FloorItemRuntimePickupStatus.Available, store.GetPickupStatus(1, 77, Now, out _));
        using var retry = Reserve(store, entry);
        Assert.False(failedSave.TryConsume(out _));
        failedSave.Dispose(); // Must not release the newer token for the same UID.
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.GetPickupStatus(1, 77, Now, out _));
        Assert.True(retry.TryConsume(out var consumed)); Assert.Same(entry, consumed);
        Assert.Equal(original, PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([consumed.Item!], [], [], [], [])));
        Assert.False(retry.TryConsume(out _));
        retry.Dispose();
        Assert.Empty(store.SnapshotVisible(Now));
    }

    [Fact]
    public void AdmittedReservationSurvivesTtlAndAllExpiryConsumersUntilExactPostSaveConsume()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1); store.AddVisible(entry, Now);
        using var token = Reserve(store, entry);
        var late = Now.AddHours(1);
        Assert.Empty(store.ConsumeExpired(late));
        Assert.Same(entry, Assert.Single(store.SnapshotVisible(late)));
        Assert.True(store.TryGetVisibleEntry(1, late, out var visible)); Assert.Same(entry, visible);
        Assert.True(store.TryGetVisiblePacket(1, late, out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.GetPickupStatus(1, 77, late, out _));
        Assert.Empty(store.ConsumeDueEffects(100, late));
        Assert.Empty(store.ConsumeDueRepeatEffects(100, late));
        var repeating = store.ConsumeDueRepeatingEffects(100, late);
        Assert.Empty(repeating.Invoked); Assert.Empty(repeating.Expired);
        Assert.True(token.TryConsume(out var consumed)); Assert.Same(entry, consumed);
        Assert.False(token.TryConsume(out _));
        Assert.Empty(store.SnapshotVisible(late));
    }

    [Fact]
    public void ReleaseAfterTtlRestoresOrdinaryExpiryWithoutRefreshingLifetime()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1); store.AddVisible(entry, Now);
        var token = Reserve(store, entry);
        Assert.Empty(store.ConsumeExpired(Now.AddSeconds(40)));
        token.Dispose();
        Assert.Same(entry, Assert.Single(store.ConsumeExpired(Now.AddSeconds(40))));
        Assert.False(token.TryConsume(out _));
        Assert.False(store.TryGetVisibleEntry(1, Now.AddSeconds(40), out _));
    }

    [Fact]
    public void AdmissionRechecksOwnerWindowAndAllowsOnlyOwnerOrExplicitBypass()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1) with { OwnerOnly = true }; store.AddVisible(entry, Now);
        Assert.Throws<ArgumentNullException>(() => store.TryReservePickup(null!, 77, false, Now, out _));
        Assert.Equal(FloorItemRuntimePickupStatus.OwnerLocked, store.TryReservePickup(entry, 88, false, Now, out var denied));
        Assert.Null(denied);
        using (var owner = Reserve(store, entry, playerUid: 77)) Assert.Same(entry, owner.Entry);
        Assert.Equal(FloorItemRuntimePickupStatus.Available, store.TryReservePickup(entry, 88, true, Now, out var bypass));
        bypass!.Dispose();
        Assert.Equal(FloorItemRuntimePickupStatus.Available, store.TryReservePickup(entry, 88, false, Now.AddSeconds(21), out var expiredLock));
        expiredLock!.Dispose();
        Assert.Equal(FloorItemRuntimePickupStatus.Available, store.TryConsumePickupEntry(1, 88, Now.AddSeconds(21), out var consumed));
        Assert.Same(entry, consumed); // Ordinary no-token pickup remains compatible.
    }

    [Fact]
    public void AlreadyExpiredEntryCannotAcquireReservationAndIsRemovedNormally()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1); store.AddVisible(entry, Now);
        Assert.Equal(FloorItemRuntimePickupStatus.Expired,
            store.TryReservePickup(entry, 77, false, Now.AddSeconds(40), out var token));
        Assert.Null(token);
        Assert.Empty(store.SnapshotVisible(Now.AddSeconds(40)));
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("map")]
    [InlineData("uid")]
    [InlineData("replacement")]
    public void ReservationRequiresExactCurrentEntryNotUidMapOrValueEquivalentObservation(string scenario)
    {
        var store = new FloorItemRuntimeStateStore();
        var original = Entry(1); store.AddVisible(original, Now);
        var expected = scenario switch
        {
            "copy" => original with { }, "map" => original with { MapId = 1015 },
            "uid" => original with { Packet = original.Packet with { Uid = 2 } }, _ => original,
        };
        var current = original;
        if (scenario == "replacement") { current = original with { Packet = original.Packet with { X = 200 } }; store.AddVisible(current, Now); }
        Assert.Equal(FloorItemRuntimePickupStatus.Missing, store.TryReservePickup(expected, 77, false, Now, out var denied));
        Assert.Null(denied);
        Assert.Same(current, Assert.Single(store.SnapshotVisible(Now)));
        using var exact = Reserve(store, current);
        Assert.True(exact.TryConsume(out var consumed)); Assert.Same(current, consumed);
    }

    [Fact]
    public void EveryOverwriteRemovalAndGroundPlacementPathRespectsReservedUidAndTile()
    {
        var store = new FloorItemRuntimeStateStore();
        var entry = Entry(1); store.AddVisible(entry, Now);
        using var token = Reserve(store, entry);
        var replacement = entry with { Packet = entry.Packet with { X = 200 } };
        Assert.Throws<InvalidOperationException>(() => store.AddVisible(replacement, Now));
        Assert.Throws<InvalidOperationException>(() => store.AddVisible(replacement));
        Assert.Throws<InvalidOperationException>(() => store.AddVisiblePacket(replacement.Packet));
        Assert.False(store.TryAddVisibleOnGround(replacement, 0, out _));
        Assert.False(store.TryAddVisibleOnGround(replacement, Now, 0, null, out _));
        Assert.False(store.TryRemoveVisible(1, out _));
        Assert.False(store.TryReserveGround(replacement, Now, 0, null, out _));
        Assert.False(store.TryReserveGround(Entry(2), Now.AddHours(1), 0,
            (_, x, y) => x == 100 && y == 100, out _));
        Assert.Same(entry, Assert.Single(store.SnapshotVisible(Now.AddHours(1))));
        Assert.True(token.TryConsume(out var consumed)); Assert.Same(entry, consumed);
    }

    [Fact]
    public void CapacityEvictionSkipsReservedOldestAndRetainsOrdinaryOrderForEligibleEntries()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 2);
        var oldest = Entry(1); store.AddVisible(oldest, Now); store.AddVisible(Entry(2, 200), Now);
        using var token = Reserve(store, oldest);
        store.AddVisible(Entry(3, 300), Now);
        Assert.Equal(new uint[] { 1, 3 }, Uids(store));
        store.AddVisible(Entry(4, 400), Now);
        Assert.Equal(new uint[] { 1, 4 }, Uids(store));
        Assert.True(token.TryConsume(out var consumed)); Assert.Same(oldest, consumed);
        Assert.Equal(new uint[] { 4 }, Uids(store));
    }

    [Fact]
    public void FullyReservedCapacityRejectsNewPublicationBoundedlyWithoutMutatingLoot()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var entry = Entry(1); store.AddVisible(entry, Now);
        var token = Reserve(store, entry);
        Assert.Throws<InvalidOperationException>(() => store.AddVisible(Entry(2, 200), Now));
        Assert.False(store.TryAddVisibleOnGround(Entry(2, 200), Now, 0, null, out _));
        Assert.False(store.TryReserveGround(Entry(2, 200), Now, 0, null, out _));
        Assert.Same(entry, Assert.Single(store.SnapshotVisible(Now)));
        token.Dispose();
        store.AddVisible(Entry(2, 200), Now);
        Assert.Equal(new uint[] { 2 }, Uids(store));
        Assert.False(token.TryConsume(out _));
    }

    [Fact]
    public void CombinedGroundAndPickupClaimsBoundCapacityAndGuaranteeGroundPublication()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 2);
        var protectedEntry = Entry(1); var evictable = Entry(2, 200);
        store.AddVisible(protectedEntry, Now); store.AddVisible(evictable, Now);
        Assert.True(store.TryReserveGround(Entry(3, 300), Now, 0, null, out var ground));
        using var groundToken = ground!;
        using var pickup = Reserve(store, protectedEntry);
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.TryReservePickup(evictable, 77, false, Now, out _));
        Assert.False(store.TryReserveGround(Entry(4, 400), Now, 0, null, out _));
        Assert.True(groundToken.TryPublish(Now)); // Evicts only the unreserved entry2.
        Assert.Equal(new uint[] { 1, 3 }, Uids(store));
        Assert.True(pickup.TryConsume(out var consumed)); Assert.Same(protectedEntry, consumed);
        Assert.Equal(new uint[] { 3 }, Uids(store));

        var one = new FloorItemRuntimeStateStore(capacity: 1);
        var old = Entry(10); one.AddVisible(old, Now);
        Assert.True(one.TryReserveGround(Entry(11, 200), Now, 0, null, out var lastSlot));
        using var pending = lastSlot!;
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, one.TryReservePickup(old, 77, false, Now, out _));
        Assert.True(pending.TryPublish(Now));
        Assert.Equal(new uint[] { 11 }, Uids(one));
    }

    [Fact]
    public void EffectsCannotBeReservedAsLootAndTheirExistingEffectConsumerStillWorks()
    {
        foreach (var visiblePacket in new[] { false, true })
        {
            var store = new FloorItemRuntimeStateStore();
            var effect = Entry(1) with
            {
                Packet = Entry(1).Packet with { DropType = visiblePacket ? LegacyFloorItemDropType.Visible : LegacyFloorItemDropType.Effect },
                Effect = new FloorItemRuntimeEffect(100, 100, 100, Now),
            };
            store.AddVisible(effect, Now);
            Assert.Equal(FloorItemRuntimePickupStatus.Missing, store.TryReservePickup(effect, 77, false, Now, out var denied));
            Assert.Null(denied);
            Assert.Same(effect, Assert.Single(store.ConsumeDueEffects(100, Now)));
            Assert.Empty(store.SnapshotVisible(Now));
        }
    }

    private static FloorItemRuntimeStateStore.PickupReservation Reserve(FloorItemRuntimeStateStore store,
        FloorItemRuntimeEntry entry, uint playerUid = 77)
    {
        Assert.Equal(FloorItemRuntimePickupStatus.Available, store.TryReservePickup(entry, playerUid, false, Now, out var token));
        return Assert.IsType<FloorItemRuntimeStateStore.PickupReservation>(token);
    }
    private static uint[] Uids(FloorItemRuntimeStateStore store) => store.SnapshotVisible(Now).Select(entry => entry.Packet.Uid).Order().ToArray();
    private static FloorItemRuntimeEntry Entry(uint uid, ushort x = 100) => FloorItemRuntimeEntry.ForItem(1002,
        new LegacyFloorItemPacket(uid, 50_000, x, 100, 40, LegacyFloorItemDropType.Visible, 0, 2, 77, 0, 0, 0, 0, 0, ""),
        new PlayerItemSnapshotItem { Uid = uid, ItemId = 50_000, StackSize = 3, Plus = 2, Enchant = 17 });
}
