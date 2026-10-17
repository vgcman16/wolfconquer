using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public enum LegacyVendorListingCostMode
{
    Gold,
    CPs,
}

public enum LegacyVendorListingFailure
{
    None,
    NonTradableItem,
    BoundItem,
    InscribedItem,
    LockedItem,
    CapacityReached,
    DuplicateItemUid,
}

public readonly record struct LegacyVendorListing(
    uint ItemUid,
    LegacyVendorListingCostMode CostMode,
    long Amount);

public sealed record LegacyVendorListingAddResult(
    bool Added,
    LegacyVendorListingFailure Failure,
    LegacyVendorListingBook Book,
    LegacyVendorListing? Listing);

public sealed record LegacyVendorListingRemoveResult(
    bool Removed,
    LegacyVendorListingBook Book,
    LegacyVendorListing? Listing);

public sealed class LegacyVendorListingBook
{
    public const int MaximumListings = 20;

    private readonly LegacyVendorListing[] _listings;

    public LegacyVendorListingBook()
        : this([])
    {
    }

    private LegacyVendorListingBook(LegacyVendorListing[] listings)
    {
        _listings = listings;
        Listings = Array.AsReadOnly(listings);
    }

    public static LegacyVendorListingBook Empty { get; } = new();

    public IReadOnlyList<LegacyVendorListing> Listings { get; }

    public int Count => _listings.Length;

    public bool IsFull => Count == MaximumListings;

    public bool Contains(uint itemUid)
    {
        return TryGetListing(itemUid, out _);
    }

    public bool TryGetListing(uint itemUid, out LegacyVendorListing listing)
    {
        foreach (var candidate in _listings)
        {
            if (candidate.ItemUid == itemUid)
            {
                listing = candidate;
                return true;
            }
        }

        listing = default;
        return false;
    }

    public LegacyVendorListingAddResult TryAdd(
        PlayerItemSnapshotItem item,
        LegacyVendorListingCostMode costMode,
        long amount)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsNonTradableItem(item.ItemId))
        {
            return Rejected(LegacyVendorListingFailure.NonTradableItem);
        }

        if (item.Bound == 1)
        {
            return Rejected(LegacyVendorListingFailure.BoundItem);
        }

        if (item.Inscribed == 1)
        {
            return Rejected(LegacyVendorListingFailure.InscribedItem);
        }

        if (item.Locked != 0)
        {
            return Rejected(LegacyVendorListingFailure.LockedItem);
        }

        if (IsFull)
        {
            return Rejected(LegacyVendorListingFailure.CapacityReached);
        }

        if (Contains(item.Uid))
        {
            return Rejected(LegacyVendorListingFailure.DuplicateItemUid);
        }

        var listing = new LegacyVendorListing(item.Uid, costMode, amount);
        var updatedListings = new LegacyVendorListing[_listings.Length + 1];
        Array.Copy(_listings, updatedListings, _listings.Length);
        updatedListings[^1] = listing;

        return new LegacyVendorListingAddResult(
            Added: true,
            Failure: LegacyVendorListingFailure.None,
            Book: new LegacyVendorListingBook(updatedListings),
            Listing: listing);
    }

    public LegacyVendorListingRemoveResult TryRemove(uint itemUid)
    {
        for (var index = 0; index < _listings.Length; index++)
        {
            if (_listings[index].ItemUid != itemUid)
            {
                continue;
            }

            var removed = _listings[index];
            var updatedListings = new LegacyVendorListing[_listings.Length - 1];
            if (index > 0)
            {
                Array.Copy(_listings, 0, updatedListings, 0, index);
            }

            if (index < _listings.Length - 1)
            {
                Array.Copy(
                    _listings,
                    index + 1,
                    updatedListings,
                    index,
                    _listings.Length - index - 1);
            }

            return new LegacyVendorListingRemoveResult(
                Removed: true,
                Book: new LegacyVendorListingBook(updatedListings),
                Listing: removed);
        }

        return new LegacyVendorListingRemoveResult(
            Removed: false,
            Book: this,
            Listing: null);
    }

    private LegacyVendorListingAddResult Rejected(LegacyVendorListingFailure failure)
    {
        return new LegacyVendorListingAddResult(
            Added: false,
            Failure: failure,
            Book: this,
            Listing: null);
    }

    private static bool IsNonTradableItem(uint itemId)
    {
        // ItemType.unabletradeitem in the decompiled source, lines 328-332.
        return itemId is
            750_000 or
            722_700 or
            3_306_885 or
            3_307_142 or
            3_600_025 or
            3_200_000 or
            729_549 or
            3_008_994 or
            3_200_005 or
            3_600_029 or
            3_007_108 or
            3_007_109 or
            3_007_110 or
            3_200_004 or
            3_600_031 or
            3_600_027 or
            3_600_024;
    }
}
