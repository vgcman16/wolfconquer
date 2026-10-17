using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVendorListingBookTests
{
    [Theory]
    [InlineData(LegacyVendorListingCostMode.Gold, 4_000_000_000L)]
    [InlineData(LegacyVendorListingCostMode.CPs, 2_000_000_000L)]
    public void TryAddUsesCopyOnWriteAndPreservesCostModeAndAmount(
        LegacyVendorListingCostMode costMode,
        long amount)
    {
        var original = new LegacyVendorListingBook();

        var result = original.TryAdd(Item(uid: 101), costMode, amount);

        Assert.True(result.Added);
        Assert.Equal(LegacyVendorListingFailure.None, result.Failure);
        Assert.NotSame(original, result.Book);
        Assert.Empty(original.Listings);

        var listing = Assert.Single(result.Book.Listings);
        Assert.Equal(101U, listing.ItemUid);
        Assert.Equal(costMode, listing.CostMode);
        Assert.Equal(amount, listing.Amount);
        Assert.Equal(listing, result.Listing);
        Assert.True(result.Book.TryGetListing(101, out var found));
        Assert.Equal(listing, found);
    }

    [Fact]
    public void TryAddRejectsBoundOneWithoutMutation()
    {
        AssertSourceRejectionWithoutMutation(
            Item(uid: 102, bound: 1),
            LegacyVendorListingFailure.BoundItem);
    }

    [Fact]
    public void TryAddRejectsInscribedOneWithoutMutation()
    {
        AssertSourceRejectionWithoutMutation(
            Item(uid: 102, inscribed: 1),
            LegacyVendorListingFailure.InscribedItem);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(byte.MaxValue)]
    public void TryAddRejectsEveryNonzeroLockedValueWithoutMutation(int locked)
    {
        AssertSourceRejectionWithoutMutation(
            Item(uid: 102, locked: checked((byte)locked)),
            LegacyVendorListingFailure.LockedItem);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    public void TryAddDoesNotBroadenBoundOrInscribedSentinelChecks(int bound, int inscribed)
    {
        var book = new LegacyVendorListingBook();

        var result = book.TryAdd(
            Item(
                uid: 103,
                bound: checked((byte)bound),
                inscribed: checked((ushort)inscribed)),
            LegacyVendorListingCostMode.Gold,
            1);

        Assert.True(result.Added);
        Assert.Single(result.Book.Listings);
    }

    [Theory]
    [InlineData(750_000U)]
    [InlineData(722_700U)]
    [InlineData(3_306_885U)]
    [InlineData(3_307_142U)]
    [InlineData(3_600_025U)]
    [InlineData(3_200_000U)]
    [InlineData(729_549U)]
    [InlineData(3_008_994U)]
    [InlineData(3_200_005U)]
    [InlineData(3_600_029U)]
    [InlineData(3_007_108U)]
    [InlineData(3_007_109U)]
    [InlineData(3_007_110U)]
    [InlineData(3_200_004U)]
    [InlineData(3_600_031U)]
    [InlineData(3_600_027U)]
    [InlineData(3_600_024U)]
    public void TryAddRejectsEveryUnableTradeItemWithoutMutation(uint itemId)
    {
        AssertSourceRejectionWithoutMutation(
            Item(uid: 102, itemId: itemId),
            LegacyVendorListingFailure.NonTradableItem);
    }

    [Theory]
    [InlineData(749_999U)]
    [InlineData(750_001U)]
    [InlineData(3_600_023U)]
    [InlineData(3_600_032U)]
    public void TryAddDoesNotBroadenUnableTradeItemSet(uint itemId)
    {
        var result = new LegacyVendorListingBook().TryAdd(
            Item(uid: 104, itemId: itemId),
            LegacyVendorListingCostMode.CPs,
            10);

        Assert.True(result.Added);
    }

    [Fact]
    public void TryAddRejectsDuplicateItemUidWithoutMutation()
    {
        var initial = new LegacyVendorListingBook().TryAdd(
            Item(uid: 200, itemId: 410_001),
            LegacyVendorListingCostMode.Gold,
            500);
        Assert.True(initial.Added);
        var book = initial.Book;
        var before = book.Listings.ToArray();

        var result = book.TryAdd(
            Item(uid: 200, itemId: 410_002),
            LegacyVendorListingCostMode.CPs,
            999);

        AssertRejectedWithoutMutation(
            result,
            book,
            before,
            LegacyVendorListingFailure.DuplicateItemUid);
    }

    [Fact]
    public void TryAddRejectsTwentyFirstListingWithoutMutation()
    {
        var book = new LegacyVendorListingBook();
        for (uint uid = 1; uid <= LegacyVendorListingBook.MaximumListings; uid++)
        {
            var add = book.TryAdd(
                Item(uid: uid),
                LegacyVendorListingCostMode.Gold,
                uid * 100L);
            Assert.True(add.Added);
            book = add.Book;
        }

        var before = book.Listings.ToArray();
        var result = book.TryAdd(
            Item(uid: 21),
            LegacyVendorListingCostMode.CPs,
            2_100);

        Assert.True(book.IsFull);
        Assert.Equal(LegacyVendorListingBook.MaximumListings, book.Count);
        AssertRejectedWithoutMutation(
            result,
            book,
            before,
            LegacyVendorListingFailure.CapacityReached);
    }

    [Fact]
    public void TryRemoveUsesCopyOnWriteAndReturnsTheRemovedListing()
    {
        var first = new LegacyVendorListingBook().TryAdd(
            Item(uid: 300),
            LegacyVendorListingCostMode.Gold,
            500).Book;
        var original = first.TryAdd(
            Item(uid: 301),
            LegacyVendorListingCostMode.CPs,
            700).Book;

        var result = original.TryRemove(300);

        Assert.True(result.Removed);
        Assert.NotSame(original, result.Book);
        Assert.Equal(2, original.Count);
        Assert.Equal(300U, result.Listing?.ItemUid);
        Assert.False(result.Book.Contains(300));
        Assert.True(result.Book.Contains(301));
    }

    [Fact]
    public void TryRemoveMissingUidDoesNotMutateTheBook()
    {
        var original = new LegacyVendorListingBook().TryAdd(
            Item(uid: 400),
            LegacyVendorListingCostMode.Gold,
            1).Book;

        var result = original.TryRemove(999);

        Assert.False(result.Removed);
        Assert.Same(original, result.Book);
        Assert.Null(result.Listing);
        Assert.True(original.Contains(400));
    }

    private static void AssertSourceRejectionWithoutMutation(
        PlayerItemSnapshotItem rejectedItem,
        LegacyVendorListingFailure expectedFailure)
    {
        var initial = new LegacyVendorListingBook().TryAdd(
            Item(uid: 101),
            LegacyVendorListingCostMode.Gold,
            100);
        Assert.True(initial.Added);
        var book = initial.Book;
        var before = book.Listings.ToArray();

        var result = book.TryAdd(
            rejectedItem,
            LegacyVendorListingCostMode.CPs,
            200);

        AssertRejectedWithoutMutation(result, book, before, expectedFailure);
    }

    private static void AssertRejectedWithoutMutation(
        LegacyVendorListingAddResult result,
        LegacyVendorListingBook book,
        IReadOnlyList<LegacyVendorListing> before,
        LegacyVendorListingFailure expectedFailure)
    {
        Assert.False(result.Added);
        Assert.Equal(expectedFailure, result.Failure);
        Assert.Same(book, result.Book);
        Assert.Null(result.Listing);
        Assert.Equal(before, book.Listings);
        Assert.Equal(before, result.Book.Listings);
    }

    private static PlayerItemSnapshotItem Item(
        uint uid,
        uint itemId = 410_001,
        byte bound = 0,
        ushort inscribed = 0,
        byte locked = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Bound = bound,
            Inscribed = inscribed,
            Locked = locked,
        };
    }
}
