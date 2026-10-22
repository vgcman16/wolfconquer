using System.Collections.Concurrent;
using WolfConquer.Core.World;
using Inventory = WolfConquer.Core.World.LegacyNativeInventoryRuntime;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;

namespace WolfConquer.Tests;

// Exact source-component queries over physical items; no live inventory/send claim.
public sealed class LegacyNativeInventoryQueriesTests
{
    private const uint OrdinaryId = 600000;

    [Fact]
    public void CountTreatsZeroStackAsOneButContainUsesRawStackAndNeedsAnEligibleMatch()
    {
        var inventory = new Inventory(null);
        var zero = Physical(1, stack: 0);
        inventory.ClientItems[1] = zero;
        inventory.ClientItems[2] = Physical(2, stack: 1);
        inventory.ClientItems[3] = Physical(3, stack: 7);
        inventory.ClientItems[4] = Physical(4, OrdinaryId + 1, 50);

        Assert.Equal(9, inventory.GetCountItem(OrdinaryId));
        Assert.True(inventory.Contain(OrdinaryId, 8));
        Assert.False(inventory.Contain(OrdinaryId, 9));
        Assert.Equal(0, inventory.GetCountItem(999999));
        Assert.False(inventory.Contain(999999, 0));
        inventory.ClientItems = new ConcurrentDictionary<uint, Item> { [700] = zero };
        Assert.Equal(1, inventory.GetCountItem(OrdinaryId));
        Assert.False(inventory.Contain(OrdinaryId, 1));
        Assert.True(inventory.Contain(OrdinaryId, 0));
        Assert.Null(inventory.Owner);
    }

    [Fact]
    public void QueriesUseValuesWithoutNormalizingKeysOrDeduplicatingSharedPhysicalItems()
    {
        var inventory = new Inventory(null);
        var shared = Physical(77, stack: 3);
        var other = Physical(88, OrdinaryId + 1, 99);
        var map = new ConcurrentDictionary<uint, Item>
        {
            [700] = shared,
            [800] = shared,
            [OrdinaryId] = other,
        };
        inventory.ClientItems = map;

        Assert.Equal(6, inventory.GetCountItem(OrdinaryId));
        Assert.True(inventory.Contain(OrdinaryId, 6));
        Assert.False(inventory.Contain(OrdinaryId, 7));
        Assert.True(inventory.SearchItemByID(OrdinaryId, out var one));
        Assert.Same(shared, one);
        Assert.True(inventory.SearchItemByID(OrdinaryId, 2, out var two));
        Assert.Equal(2, two!.Count);
        Assert.All(two, item => Assert.Same(shared, item));
        Assert.Same(map, inventory.ClientItems);
        Assert.False(map.ContainsKey(shared.Uid));
        Assert.Equal(77u, shared.Uid);
        Assert.Same(other, map[OrdinaryId]);
    }

    [Theory]
    [InlineData(600000u, (byte)0, (byte)2, false)]
    [InlineData(600000u, (byte)1, (byte)2, true)]
    [InlineData(1088001u, (byte)0, (byte)2, true)]
    [InlineData(721080u, (byte)0, (byte)2, true)]
    public void BoundZeroDiffersBetweenOrdinaryAndSpecialFamiliesWhilePositiveAcceptsAnyPositive(
        uint id, byte requestedBound, byte itemBound, bool expected)
    {
        var inventory = new Inventory(null);
        var item = Physical(1, id, 2, itemBound);
        inventory.ClientItems[900] = item;

        Assert.Equal(expected, inventory.Contain(id, 2, requestedBound));
        Assert.Same(item, inventory.ClientItems[900]);
        Assert.Equal(itemBound, item.Bound);
        Assert.Null(inventory.Owner);
    }

    [Fact]
    public void MeteorIdsPoolTogetherButSecondSpecialFamilyKeepsExactIdsAndBoundFiltering()
    {
        var inventory = new Inventory(null);
        inventory.ClientItems[1] = Physical(1, 1088001, 2, 0);
        inventory.ClientItems[2] = Physical(2, 1088002, 3, 2);
        inventory.ClientItems[3] = Physical(3, 721080, 2, 0);
        inventory.ClientItems[4] = Physical(4, 723087, 3, 2);

        Assert.True(inventory.Contain(1088001, 5));
        Assert.True(inventory.Contain(1088002, 5));
        Assert.False(inventory.Contain(1088001, 6));
        Assert.True(inventory.Contain(1088001, 3, bound: 1));
        Assert.False(inventory.Contain(1088002, 4, bound: 1));
        Assert.Equal(2, inventory.GetCountItem(1088001));
        Assert.Equal(3, inventory.GetCountItem(1088002));
        Assert.True(inventory.Contain(721080, 2));
        Assert.False(inventory.Contain(721080, 3));
        Assert.True(inventory.Contain(723087, 3));
        Assert.False(inventory.Contain(723087, 4));
        Assert.False(inventory.Contain(721080, 1, bound: 2));
        Assert.True(inventory.Contain(723087, 3, bound: 1));
    }

    [Theory]
    [InlineData(39, (byte)1, true)]
    [InlineData(40, (byte)0, true)]
    [InlineData(40, (byte)1, false)]
    [InlineData(41, (byte)0, false)]
    public void CapacityCountsDictionaryEntriesIncludingAliasesAtTheNativeFortyBoundary(
        int occupied, byte requested, bool expected)
    {
        var inventory = new Inventory(null);
        var shared = Physical(777, stack: ushort.MaxValue);
        for (uint key = 0; key < occupied; key++) inventory.ClientItems[key] = shared;

        Assert.Equal(expected, inventory.HaveSpace(requested));
        Assert.Equal(occupied, inventory.ClientItems.Count);
        Assert.All(inventory.ClientItems.Values, item => Assert.Same(shared, item));
    }

    [Fact]
    public void SearchReturnsPhysicalMatchesNotStackUnitsAndFailureDiscardsPartialList()
    {
        var inventory = new Inventory(null);
        Assert.False(inventory.SearchItemByID(OrdinaryId, out var absent));
        Assert.Null(absent);
        Assert.False(inventory.SearchItemByID(OrdinaryId, 0, out var emptyZero));
        Assert.Null(emptyZero);
        Assert.True(inventory.VerifiedUpdateItem([], OrdinaryId, 0, out var emptyVerified));
        Assert.Empty(emptyVerified);
        var item = Physical(7, stack: 500);
        inventory.ClientItems[100] = item;

        Assert.True(inventory.SearchItemByID(OrdinaryId, out var found));
        Assert.Same(item, found);
        Assert.True(inventory.SearchItemByID(OrdinaryId, 1, out var single));
        Assert.Same(item, Assert.Single(single!));
        Assert.False(inventory.SearchItemByID(OrdinaryId, 2, out var insufficient));
        Assert.Null(insufficient);
        Assert.False(inventory.SearchItemByID(OrdinaryId + 1, out var wrongType));
        Assert.Null(wrongType);
        Assert.Same(item, inventory.ClientItems[100]);
    }

    [Fact]
    public void ZeroByteCountsFailAfterOneMatchButWrapAfterTwoHundredFiftySixAliases()
    {
        var inventory = new Inventory(null);
        var shared = Physical(777);
        inventory.ClientItems[0] = shared;
        var oneRequested = new List<uint> { shared.Uid };
        Assert.False(inventory.SearchItemByID(OrdinaryId, 0, out var oneSearch));
        Assert.Null(oneSearch);
        Assert.False(inventory.VerifiedUpdateItem(oneRequested, OrdinaryId, 0, out var oneVerified));
        Assert.Empty(oneRequested);
        Assert.Same(shared, Assert.Single(oneVerified));
        for (uint key = 1; key < 256; key++) inventory.ClientItems[key] = shared;
        var requested = Enumerable.Repeat(shared.Uid, 256).ToList();

        Assert.True(inventory.SearchItemByID(OrdinaryId, 0, out var wrappedSearch));
        Assert.Equal(256, wrappedSearch!.Count);
        Assert.All(wrappedSearch, item => Assert.Same(shared, item));
        Assert.True(inventory.VerifiedUpdateItem(requested, OrdinaryId, 0, out var wrappedVerified));
        Assert.Empty(requested);
        Assert.Equal(256, wrappedVerified.Count);
        Assert.All(wrappedVerified, item => Assert.Same(shared, item));
        Assert.Equal(256, inventory.ClientItems.Count);
        Assert.Equal(777u, shared.Uid);
    }

    [Fact]
    public void FailedFinalVerificationStillConsumesFoundUidsAndReturnsSamePhysicalItems()
    {
        var inventory = new Inventory(null);
        var first = Physical(31);
        var second = Physical(32);
        var wrongType = Physical(33, OrdinaryId + 1);
        inventory.ClientItems[100] = first;
        inventory.ClientItems[200] = second;
        inventory.ClientItems[300] = wrongType;
        var requested = new List<uint> { first.Uid, second.Uid, wrongType.Uid };

        Assert.False(inventory.VerifiedUpdateItem(requested, OrdinaryId, 2, out var selected));

        Assert.Equal(new[] { wrongType.Uid }, requested);
        Assert.Equal(2, selected.Count);
        Assert.Same(first, Assert.Single(selected, item => item.Uid == 31));
        Assert.Same(second, Assert.Single(selected, item => item.Uid == 32));
        Assert.DoesNotContain(wrongType, selected);
        Assert.Equal(3, inventory.ClientItems.Count);
        Assert.Same(first, inventory.ClientItems[100]);
        Assert.Same(second, inventory.ClientItems[200]);
    }

    [Fact]
    public void ExhaustingTheRequestedUidListDoesNotRepairAnIncorrectCount()
    {
        var inventory = new Inventory(null);
        var item = Physical(10, stack: 99);
        inventory.ClientItems[200] = item;
        var requested = new List<uint> { item.Uid };

        Assert.False(inventory.VerifiedUpdateItem(requested, OrdinaryId, 2, out var selected));

        Assert.Empty(requested);
        Assert.Same(item, Assert.Single(selected));
        Assert.Equal((ushort)99, item.StackSize);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void DuplicateRequestedUidsAreConsumedOncePerDictionaryValueNotDeduplicated(
        int aliases, bool expected)
    {
        var inventory = new Inventory(null);
        var shared = Physical(77);
        for (uint key = 0; key < aliases; key++) inventory.ClientItems[key + 100] = shared;
        var requested = new List<uint> { shared.Uid, shared.Uid };

        Assert.Equal(expected, inventory.VerifiedUpdateItem(requested, OrdinaryId, 2, out var selected));

        Assert.Equal(2 - aliases, requested.Count);
        Assert.All(requested, uid => Assert.Equal(shared.Uid, uid));
        Assert.Equal(aliases, selected.Count);
        Assert.All(selected, item => Assert.Same(shared, item));
        Assert.Equal(aliases, inventory.ClientItems.Count);
    }

    [Theory]
    [InlineData(1100003u)]
    [InlineData(1100006u)]
    [InlineData(1100009u)]
    public void SashQueryRequiresAnExactSashIdAndANonemptyPhysicalDepositOwner(uint sashId)
    {
        var inventory = new Inventory(null);
        Assert.False(inventory.HaveItemsInSash());
        var child = Physical(55);
        var nonsash = Physical(10, 1100008);
        nonsash.Deposite[999] = child;
        inventory.ClientItems[100] = nonsash;
        Assert.False(inventory.HaveItemsInSash());
        var sash = Physical(20, sashId);
        inventory.ClientItems[200] = sash;
        Assert.False(inventory.HaveItemsInSash());

        sash.Deposite[888] = child;
        Assert.True(inventory.HaveItemsInSash());
        Assert.Same(child, sash.Deposite[888]);
        sash.Deposite.Clear();
        Assert.False(inventory.HaveItemsInSash());
        Assert.Same(child, nonsash.Deposite[999]);
        Assert.Null(inventory.Owner);
    }

    private static Item Physical(uint uid, uint id = OrdinaryId, ushort stack = 1, byte bound = 0) =>
        new() { Uid = uid, ITEM_ID = id, StackSize = stack, Bound = bound };
}
