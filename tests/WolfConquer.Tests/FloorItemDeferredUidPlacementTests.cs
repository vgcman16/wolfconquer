using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual store ordering/collision proof. Callbacks expose completed draw order;
// no native counter/RNG replacement, host wiring, or client rendering is claimed.
public sealed class FloorItemDeferredUidPlacementTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");

    [Fact]
    public void AllocatorProducedZeroIsARealUidAndSucceedsAfterValidMoneyPayload()
    {
        var store = new FloorItemRuntimeStateStore();
        var draft = Draft();
        var events = new List<string>();

        Assert.True(store.TryAddMoneyVisibleOnGround(draft, Now, 0, null,
            () =>
            {
                events.Add("money");
                Assert.Empty(store.SnapshotVisible(Now));
                return (1090001u, 31u);
            },
            () =>
            {
                events.Add("uid");
                Assert.Empty(store.SnapshotVisible(Now));
                return 0;
            }, out var added));

        Assert.Equal(new[] { "money", "uid" }, events);
        Assert.Equal(draft with
        {
            Amount = 31,
            Packet = draft.Packet with { Uid = 0, ItemId = 1090001 },
        }, added);
        Assert.Same(added, Assert.Single(store.SnapshotVisible(Now)));
        Assert.True(store.TryGetVisibleEntry(0, Now, out var stored));
        Assert.Same(added, stored);
        Assert.Equal(0, draft.Amount);
        Assert.Equal(0u, draft.Packet.ItemId);
    }

    [Fact]
    public void PlacementThenMoneyThenUidPreservesAllOtherFieldsAndNeverPublishesDraft()
    {
        var store = new FloorItemRuntimeStateStore();
        var existing = Existing(77, 100, 100);
        store.AddVisible(existing, Now);
        var draft = Draft();
        var events = new List<string>();

        Assert.True(store.TryAddMoneyVisibleOnGround(draft, Now, 0,
            (map, x, y) =>
            {
                Assert.Equal((1002L, (ushort)99, (ushort)99), (map, x, y));
                events.Add("ground");
                return true;
            },
            () =>
            {
                events.Add("money");
                Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
                Assert.False(store.TryGetVisibleEntry(0, Now, out _));
                return (1090020u, uint.MaxValue);
            },
            () =>
            {
                events.Add("uid");
                Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
                Assert.False(store.TryGetVisibleEntry(0, Now, out _));
                return uint.MaxValue;
            }, out var added));

        Assert.Equal(new[] { "ground", "money", "uid" }, events);
        Assert.Equal(draft with
        {
            Amount = uint.MaxValue,
            Packet = draft.Packet with { Uid = uint.MaxValue, ItemId = 1090020, X = 99, Y = 99 },
        }, added);
        Assert.Equal(FloorItemRuntimePickupKind.Money, added.Kind);
        Assert.True(added.OwnerOnly);
        Assert.Null(added.Item);
        Assert.Null(added.Effect);
        Assert.False(store.TryGetVisibleEntry(0, Now, out _));
        Assert.True(store.TryGetVisibleEntry(uint.MaxValue, Now, out var stored));
        Assert.Same(added, stored);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidGroundOrAllPinnedCapacitySkipsBothMoneyAndUidDraws(bool pinnedCapacity)
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Existing(77);
        store.AddVisible(existing, Now);
        using var pickup = pinnedCapacity ? ReservePickup(store, existing) : null;
        int moneyCalls = 0;
        int uidCalls = 0;

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(), Now, 0,
            (_, _, _) => pinnedCapacity,
            () => { moneyCalls++; return (1090001u, 31u); },
            () => { uidCalls++; return 81; }, out var added));

        Assert.Equal(0, moneyCalls);
        Assert.Equal(0, uidCalls);
        Assert.Null(added);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Theory]
    [InlineData("catalog-null")]
    [InlineData("zero-item")]
    [InlineData("zero-amount")]
    public void MissingOrInvalidMoneyPayloadSkipsUidDrawAndDoesNotEvict(string result)
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Existing(77);
        store.AddVisible(existing, Now);
        int moneyCalls = 0;
        int uidCalls = 0;
        (uint ItemId, uint Amount)? payload = result switch
        {
            "catalog-null" => null,
            "zero-item" => (0u, 31u),
            _ => (1090001u, 0u),
        };

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null,
            () => { moneyCalls++; return payload; },
            () => { uidCalls++; return 81; }, out var added));

        Assert.Equal(1, moneyCalls);
        Assert.Equal(0, uidCalls);
        Assert.Null(added);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
    }

    [Theory]
    [InlineData("visible", 0u)]
    [InlineData("visible", 77u)]
    [InlineData("ground", 0u)]
    [InlineData("ground", 77u)]
    [InlineData("pickup", 0u)]
    [InlineData("pickup", 77u)]
    public void AllocatedUidCollisionRejectsOnceWithoutOverwriteRetryOrEviction(string owner, uint uid)
    {
        var store = new FloorItemRuntimeStateStore(capacity: owner == "visible" ? 1 : 2);
        var existing = Existing(uid);
        if (owner != "ground") store.AddVisible(existing, Now);
        using var ground = owner == "ground" ? ReserveGround(store, existing) : null;
        using var pickup = owner == "pickup" ? ReservePickup(store, existing) : null;
        var events = new List<string>();

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null,
            () => { events.Add("money"); return (1090001u, 31u); },
            () => { events.Add("uid"); return uid; }, out var added));

        Assert.Equal(new[] { "money", "uid" }, events);
        Assert.Null(added);
        if (ground is not null)
        {
            Assert.Empty(store.SnapshotVisible(Now));
            Assert.True(ground.TryPublish(Now));
            Assert.Same(ground.Entry, Assert.Single(store.SnapshotVisible(Now)));
        }
        else
        {
            Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
            if (pickup is not null)
                Assert.Equal(FloorItemRuntimePickupStatus.Busy, store.GetPickupStatus(uid, 77, Now, out _));
        }
    }

    [Theory]
    [InlineData("visible")]
    [InlineData("ground")]
    [InlineData("pickup")]
    public void UndrawnDraftZeroIsNotAnIdentityAndDoesNotConflictWithExistingZeroUid(string owner)
    {
        var store = new FloorItemRuntimeStateStore(capacity: 3);
        var existing = Existing(0);
        if (owner != "ground") store.AddVisible(existing, Now);
        using var ground = owner == "ground" ? ReserveGround(store, existing) : null;
        using var pickup = owner == "pickup" ? ReservePickup(store, existing) : null;
        int uidCalls = 0;

        Assert.True(store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null,
            () => (1090001u, 31u),
            () => { uidCalls++; return 90; }, out var added));

        Assert.Equal(1, uidCalls);
        Assert.Equal(90u, added.Packet.Uid);
        Assert.Equal(31, added.Amount);
        if (ground is not null)
        {
            Assert.Same(added, Assert.Single(store.SnapshotVisible(Now)));
            Assert.True(ground.TryPublish(Now));
            Assert.True(store.TryGetVisibleEntry(0, Now, out var retained));
            Assert.Same(ground.Entry, retained);
        }
        else
        {
            Assert.True(store.TryGetVisibleEntry(0, Now, out var retained));
            Assert.Same(existing, retained);
        }
        Assert.Equal(new uint[] { 0, 90 }, Uids(store));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MoneyOrUidCallbackFaultDoesNotPublishDraftOrEvictOldEntry(bool failUid)
    {
        var store = new FloorItemRuntimeStateStore(capacity: 1);
        var existing = Existing(77);
        store.AddVisible(existing, Now);
        var failure = new InvalidOperationException("synthetic deferred generation fault");
        int moneyCalls = 0;
        int uidCalls = 0;

        var actual = Assert.Throws<InvalidOperationException>(() =>
            store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null,
                () =>
                {
                    moneyCalls++;
                    if (!failUid) throw failure;
                    return (1090001u, 31u);
                },
                () => { uidCalls++; throw failure; }, out _));

        Assert.Same(failure, actual);
        Assert.Equal(1, moneyCalls);
        Assert.Equal(failUid ? 1 : 0, uidCalls);
        Assert.Same(existing, Assert.Single(store.SnapshotVisible(Now)));
        Assert.False(store.TryGetVisibleEntry(0, Now, out _));
    }

    [Fact]
    public void RepeatedPilesDrawOneQueuedUidPerSuccessfulPayloadAndNoneForBlockedPlacement()
    {
        var store = new FloorItemRuntimeStateStore();
        var amounts = new Queue<uint>(new uint[] { 31, 107 });
        var uids = new Queue<uint>(new uint[] { 91, 92 });
        var events = new List<string>();
        (uint ItemId, uint Amount)? Money()
        {
            events.Add("money");
            return (1090001u, amounts.Dequeue());
        }
        uint Uid() { events.Add("uid"); return uids.Dequeue(); }

        Assert.False(store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, (_, _, _) => false, Money, Uid, out _));
        Assert.Empty(events);
        Assert.True(store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null, Money, Uid, out var first));
        Assert.True(store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null, Money, Uid, out var second));

        Assert.Equal(new[] { "money", "uid", "money", "uid" }, events);
        Assert.Empty(amounts);
        Assert.Empty(uids);
        Assert.Equal((91u, 31L, (ushort)100, (ushort)100),
            (first.Packet.Uid, first.Amount, first.Packet.X, first.Packet.Y));
        Assert.Equal((92u, 107L, (ushort)99, (ushort)99),
            (second.Packet.Uid, second.Amount, second.Packet.X, second.Packet.Y));
        Assert.Equal(new uint[] { 91, 92 }, Uids(store));
    }

    [Fact]
    public void ExistingPublicAndMoneyOnlyOverloadsKeepCallerUidAndReplacementBehavior()
    {
        var store = new FloorItemRuntimeStateStore(capacity: 2);
        var ordinary = FloorItemRuntimeEntry.ForMoney(1002,
            Draft().Packet with { Uid = 41, ItemId = 1090002, X = 20, Y = 20 }, 333);
        Assert.True(store.TryAddVisibleOnGround(ordinary, Now, 0, null, out var first));
        Assert.Equal(ordinary, first);

        var draft = Draft() with { Packet = Draft().Packet with { Uid = 42 } };
        Assert.True(store.TryAddMoneyVisibleOnGround(draft, Now, 0, null,
            () => (1090001u, 31u), out var second));
        Assert.Equal(42u, second.Packet.Uid);
        Assert.Equal(31, second.Amount);
        var replacement = draft with { Packet = draft.Packet with { X = 200, Y = 200 } };
        Assert.True(store.TryAddMoneyVisibleOnGround(replacement, Now, 0, null,
            () => (1090010u, 777u), out var third));

        Assert.Equal(42u, third.Packet.Uid);
        Assert.Equal(777, third.Amount);
        Assert.Equal(new uint[] { 41, 42 }, Uids(store));
        Assert.True(store.TryGetVisibleEntry(42, Now, out var stored));
        Assert.Same(third, stored);
    }

    [Fact]
    public void NewOverloadValidatesCallbacksKindAndUndrawnUidWithoutInvokingFactories()
    {
        var store = new FloorItemRuntimeStateStore();
        int moneyCalls = 0;
        int uidCalls = 0;
        (uint ItemId, uint Amount)? Money() { moneyCalls++; return (1090001u, 31u); }
        uint Uid() { uidCalls++; return 81; }
        Assert.Throws<ArgumentNullException>(() =>
            store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null, Money, null!, out _));
        Assert.Throws<ArgumentNullException>(() =>
            store.TryAddMoneyVisibleOnGround(Draft(), Now, 0, null, null!, Uid, out _));
        Assert.Throws<ArgumentException>(() =>
            store.TryAddMoneyVisibleOnGround(Draft() with { Kind = FloorItemRuntimePickupKind.Item },
                Now, 0, null, Money, Uid, out _));
        Assert.Throws<ArgumentException>(() =>
            store.TryAddMoneyVisibleOnGround(Draft() with { Packet = Draft().Packet with { Uid = 7 } },
                Now, 0, null, Money, Uid, out _));

        Assert.Equal(0, moneyCalls);
        Assert.Equal(0, uidCalls);
        Assert.Empty(store.SnapshotVisible(Now));
    }

    private static FloorItemRuntimeStateStore.GroundReservation ReserveGround(
        FloorItemRuntimeStateStore store, FloorItemRuntimeEntry entry)
    {
        Assert.True(store.TryReserveGround(entry, Now, 0, null, out var pending));
        return Assert.IsType<FloorItemRuntimeStateStore.GroundReservation>(pending);
    }

    private static FloorItemRuntimeStateStore.PickupReservation ReservePickup(
        FloorItemRuntimeStateStore store, FloorItemRuntimeEntry entry)
    {
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            store.TryReservePickup(entry, 77, false, Now, out var pending));
        return Assert.IsType<FloorItemRuntimeStateStore.PickupReservation>(pending);
    }

    private static uint[] Uids(FloorItemRuntimeStateStore store) =>
        store.SnapshotVisible(Now).Select(entry => entry.Packet.Uid).Order().ToArray();

    private static FloorItemRuntimeEntry Existing(uint uid, ushort x = 10, ushort y = 10) =>
        FloorItemRuntimeEntry.ForItem(1002,
            Draft().Packet with { Uid = uid, ItemId = 50000, X = x, Y = y });

    private static FloorItemRuntimeEntry Draft() =>
        new(1002, new LegacyFloorItemPacket(0, 0, 100, 100, 40, LegacyFloorItemDropType.Visible,
            37, 2, 77, 91, 3, 123456789, 7, 8, "cash"),
            FloorItemRuntimePickupKind.Money, Amount: 0, Item: null, OwnerOnly: true);
}
