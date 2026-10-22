using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real in-process store proof. No native RNG implementation, packet socket,
// managed monster-death integration, restart, or live-client claim follows.
public sealed class FloorItemDeferredMoneyPlacementTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    [Fact]
    public void BlockedTerrainDoesNotGeneratePayloadOrEvictExistingEntry()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Visible(1, 10, 10);
        store.AddVisible(existing, Now);
        int calls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(2), Now, 0,
            (_, _, _) => false, () => { calls++; return (1090001u, 31u); }, out var added));

        Assert.Equal(0, calls);
        Assert.Null(added);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Fact]
    public void FullyOccupiedAllowedTerrainDoesNotGeneratePayload()
    {
        var store = new FloorItemRuntimeStateStore();
        var existing = Visible(1);
        store.AddVisible(existing, Now);
        int calls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(2), Now, 0,
            (_, x, y) => x == 100 && y == 100,
            () => { calls++; return (1090001u, 31u); }, out var added));

        Assert.Equal(0, calls);
        Assert.Null(added);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PendingGroundUidOrOnlyAllowedTileRejectsWithoutGenerating(bool sameUid)
    {
        var store = new FloorItemRuntimeStateStore();
        Assert.True(store.TryReserveGround(Visible(1), Now, 0, null, out var pending));
        using var token = pending!;
        var draft = sameUid ? Draft(1, 200, 200) : Draft(2);
        int calls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(draft, Now, 0,
            (_, x, y) => x == draft.Packet.X && y == draft.Packet.Y,
            () => { calls++; return (1090001u, 31u); }, out var added));

        Assert.Equal(0, calls);
        Assert.Null(added);
        Assert.Empty(store.SnapshotVisible(Now));
        Assert.True(token.TryPublish(Now));
        Assert.Same(token.Entry, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PendingPickupUidOrOnlyAllowedTileRemainsPinnedEvenPastTtl(bool sameUid)
    {
        var store = new FloorItemRuntimeStateStore();
        var existing = Visible(1);
        store.AddVisible(existing, Now);
        using var token = ReservePickup(store, existing);
        var draft = sameUid ? Draft(1, 200, 200) : Draft(2);
        var late = Now.AddHours(1);
        int calls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(draft, late, 0,
            (_, x, y) => x == draft.Packet.X && y == draft.Packet.Y,
            () => { calls++; return (1090001u, 31u); }, out var added));

        Assert.Equal(0, calls);
        Assert.Null(added);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(late)));
        Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.GetPickupStatus(1, 77, late, out _));
        Assert.True(token.TryConsume(out var consumed));
        Assert.Same(existing, consumed);
        Assert.Empty(store.SnapshotVisible(late));
    }

    [Fact]
    public void CapacityEntirelyPinnedForPickupRejectsBeforeGeneratingAndCanRetryAfterRelease()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 2);
        var first = Visible(1, 10, 10);
        var second = Visible(2, 20, 20);
        store.AddVisible(first, Now);
        store.AddVisible(second, Now);
        using var firstToken = ReservePickup(store, first);
        using var secondToken = ReservePickup(store, second);
        int calls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(3), Now, 0, null,
            () => { calls++; return (1090001u, 31u); }, out var rejected));

        Assert.Equal(0, calls);
        Assert.Null(rejected);
        Assert.Equal(new uint[] { 1, 2 }, Uids(store, Now));
        secondToken.Dispose();
        Assert.True(store.TryAddMoneyVisibleOnGround(Draft(3), Now, 0, null,
            () => { calls++; return (1090001u, 31u); }, out var added));
        Assert.Equal(1, calls);
        Assert.Equal(31, added.Amount);
        Assert.Equal(new uint[] { 1, 3 }, Uids(store, Now));
        Assert.Same(first, firstToken.Entry);
    }

    [Fact]
    public void FactoryRunsExactlyOnceAfterScatterSelectionAndCannotObservePlaceholderPublication()
    {
        var store = new FloorItemRuntimeStateStore();
        var existing = Visible(1);
        store.AddVisible(existing, Now);
        var groundChecks = new List<(long Map, ushort X, ushort Y)>();
        int calls = 0;
        var draft = Draft(2);

        Assert.True(store.TryAddMoneyVisibleOnGround(draft, Now, 0,
            (map, x, y) => { groundChecks.Add((map, x, y)); return true; },
            () =>
            {
                calls++;
                Assert.Equal((1002L, (ushort)99, (ushort)99), Assert.Single(groundChecks));
                Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
                Assert.False(store.TryGetVisibleEntry(2, Now, out _));
                return (1090010u, 12345u);
            }, out var added));

        Assert.Equal(1, calls);
        Assert.Equal((99, 99), ((int)added.Packet.X, (int)added.Packet.Y));
        Assert.Equal(draft with
        {
            Amount = 12345,
            Packet = draft.Packet with { ItemId = 1090010, X = 99, Y = 99 },
        }, added);
        Assert.True(store.TryGetVisibleEntry(2, Now, out var stored));
        Assert.Same(added, stored);
        Assert.All(store.SnapshotVisible(Now), entry => Assert.NotEqual(0u, entry.Packet.ItemId));
        Assert.Equal(0, draft.Amount);
        Assert.Equal(0u, draft.Packet.ItemId);
    }

    [Fact]
    public void SuccessfulFactoryReplacesOnlyPayloadAndEvictsOnlyAfterItReturns()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Visible(1, 10, 10);
        store.AddVisible(existing, Now);
        var draft = Draft(2) with { OwnerOnly = true };
        int calls = 0;

        Assert.True(store.TryAddMoneyVisibleOnGround(draft, Now, 0, null,
            () =>
            {
                calls++;
                Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
                return (1090020u, uint.MaxValue);
            }, out var added));

        Assert.Equal(1, calls);
        Assert.Equal(draft with
        {
            Amount = uint.MaxValue,
            Packet = draft.Packet with { ItemId = 1090020 },
        }, added);
        Assert.Same(added, Assert.Single(store.SnapshotVisible(Now)));
        Assert.False(store.TryGetVisibleEntry(1, Now, out _));
        Assert.Null(added.Item);
        Assert.Null(added.Effect);
        Assert.True(added.OwnerOnly);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullFactoryResultDoesNotPublishEvictOrOverwriteExistingUid(bool sameUid)
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Visible(1, 10, 10);
        store.AddVisible(existing, Now);
        var draft = Draft(sameUid ? 1u : 2u);
        int calls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(draft, Now, 0, null,
            () => { calls++; return null; }, out var added));

        Assert.Equal(1, calls);
        Assert.Null(added);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryExceptionPropagatesWithoutPublicationEvictionOrOverwrite(bool sameUid)
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Visible(1, 10, 10);
        store.AddVisible(existing, Now);
        var draft = Draft(sameUid ? 1u : 2u);
        var failure = new InvalidOperationException("synthetic payload generation failure");
        int calls = 0;

        var actual = Assert.Throws<InvalidOperationException>(() =>
            store.TryAddMoneyVisibleOnGround(draft, Now, 0, null,
                () => { calls++; throw failure; }, out _));

        Assert.Same(failure, actual);
        Assert.Equal(1, calls);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Theory]
    [InlineData(0u, 31u)]
    [InlineData(1090001u, 0u)]
    public void ZeroGeneratedIdOrAmountRejectsWithoutPublishingOrEvicting(uint itemId, uint amount)
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Visible(1, 10, 10);
        store.AddVisible(existing, Now);
        int calls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(2), Now, 0, null,
            () => { calls++; return (itemId, amount); }, out var added));

        Assert.Equal(1, calls);
        Assert.Null(added);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Fact]
    public void RepeatedSuccessfulPilesConsumeDistinctQueuedPayloadsWithoutPlaceholderReuse()
    {
        var store = new FloorItemRuntimeStateStore();
        var payloads = new Queue<(uint ItemId, uint Amount)>(new[]
        {
            (1090001u, 31u), (1090002u, 107u), (1090010u, 1003u),
        });
        var addedEntries = new List<FloorItemRuntimeEntry>();
        int calls = 0;
        for (uint uid = 1; uid <= 3; uid++)
        {
            Assert.True(store.TryAddMoneyVisibleOnGround(Draft(uid), Now, 0, null,
                () => { calls++; return payloads.Dequeue(); }, out var added));
            addedEntries.Add(added);
        }

        Assert.Equal(3, calls);
        Assert.Empty(payloads);
        Assert.Equal(new long[] { 31, 107, 1003 }, addedEntries.Select(entry => entry.Amount));
        Assert.Equal(new uint[] { 1090001, 1090002, 1090010 }, addedEntries.Select(entry => entry.Packet.ItemId));
        Assert.Equal(new[] { (100, 100), (99, 99), (100, 99) },
            addedEntries.Select(entry => ((int)entry.Packet.X, (int)entry.Packet.Y)));
        Assert.Equal(new uint[] { 1, 2, 3 }, Uids(store, Now));
        foreach (var added in addedEntries)
        {
            Assert.True(store.TryGetVisibleEntry(added.Packet.Uid, Now, out var stored));
            Assert.Same(added, stored);
        }
    }

    [Fact]
    public void FailedPlacementDoesNotConsumeNextQueuedPayload()
    {
        var store = new FloorItemRuntimeStateStore();
        var payloads = new Queue<(uint ItemId, uint Amount)>(new[] { (1090001u, 31u), (1090002u, 107u) });
        int calls = 0;
        (uint ItemId, uint Amount)? Generate() { calls++; return payloads.Dequeue(); }

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(1), Now, 0, (_, _, _) => false, Generate, out _));
        Assert.Equal(0, calls);
        Assert.Equal(2, payloads.Count);
        Assert.True(store.TryAddMoneyVisibleOnGround(Draft(1), Now, 0, null, Generate, out var first));
        Assert.True(store.TryAddMoneyVisibleOnGround(Draft(2), Now, 0, null, Generate, out var second));
        Assert.Equal(31, first.Amount);
        Assert.Equal(107, second.Amount);
        Assert.Equal(2, calls);
        Assert.Empty(payloads);
    }

    [Fact]
    public void UnreservedExpiredEntryIsRemovedBeforeGenerationAndNewLifetimeStartsAtPublication()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        store.AddVisible(Visible(1), Now);
        var late = Now.AddSeconds(40);
        var draft = Draft(2) with { OwnerOnly = true };
        int calls = 0;

        Assert.True(store.TryAddMoneyVisibleOnGround(draft, late, 0, null,
            () =>
            {
                calls++;
                Assert.Empty(store.SnapshotVisible(late));
                return (1090001u, 31u);
            }, out var added));

        Assert.Equal(1, calls);
        Assert.Equal((100, 100), ((int)added.Packet.X, (int)added.Packet.Y));
        Assert.Equal(FloorItemRuntimePickupStatus.OwnerLocked,
            store.GetPickupStatus(2, 88, late.AddSeconds(20), out _));
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            store.GetPickupStatus(2, 88, late.AddSeconds(21), out _));
        Assert.Same(added, Assert.Single(store.SnapshotVisible(late.AddSeconds(39))));
        Assert.Same(added, Assert.Single(store.ConsumeExpired(late.AddSeconds(40))));
        Assert.Empty(store.SnapshotVisible(late.AddSeconds(40)));
    }

    [Fact]
    public void ExistingPublicGroundAddKeepsOrdinaryMoneyAndSameUidReplacementSemantics()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var original = Visible(1);
        store.AddVisible(original, Now);
        var ordinary = FloorItemRuntimeEntry.ForMoney(1002,
            Draft(1, 200, 200).Packet with { ItemId = 1090002 }, 333, ownerOnly: true);

        Assert.True(store.TryAddVisibleOnGround(ordinary, Now, 0, null, out var added));

        Assert.Equal(ordinary, added);
        Assert.Same(added, Assert.Single(store.SnapshotVisible(Now)));
        Assert.Equal(333, added.Amount);
        Assert.Equal(1090002u, added.Packet.ItemId);
    }

    private static FloorItemRuntimeStateStore.PickupReservation ReservePickup(
        FloorItemRuntimeStateStore store, FloorItemRuntimeEntry entry)
    {
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            store.TryReservePickup(entry, 77, false, Now, out var pending));
        return Assert.IsType<FloorItemRuntimeStateStore.PickupReservation>(pending);
    }

    private static uint[] Uids(FloorItemRuntimeStateStore store, DateTimeOffset now) =>
        store.SnapshotVisible(now).Select(entry => entry.Packet.Uid).Order().ToArray();

    private static FloorItemRuntimeEntry Visible(uint uid, ushort x = 100, ushort y = 100) =>
        FloorItemRuntimeEntry.ForItem(1002, Draft(uid, x, y).Packet with { ItemId = 50000 });

    private static FloorItemRuntimeEntry Draft(uint uid, ushort x = 100, ushort y = 100) =>
        new(1002, new LegacyFloorItemPacket(uid, 0, x, y, 40, LegacyFloorItemDropType.Visible,
            37, 2, 77, 91, 3, 123456789, 7, 8, "cash"),
            FloorItemRuntimePickupKind.Money, Amount: 0, Item: null, OwnerOnly: false);
}
