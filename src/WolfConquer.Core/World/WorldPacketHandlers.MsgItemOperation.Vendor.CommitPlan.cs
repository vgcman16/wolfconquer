using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryValidateLegacyVendorTransfer(WorldSession buyer, WorldSession updatedBuyer,
        WorldSession seller, WorldSession updatedSeller, LegacyItemSaveGraph buyerItems,
        LegacyItemSaveGraph sellerItems, LegacyItemSaveGraph preparedBuyerItems,
        LegacyItemSaveGraph preparedSellerItems, out LegacyItemSaveGraph.Closure incoming, out string reason)
    {
        incoming = null!;
        reason = "vendorPurchasePlanInvalid";
        if (buyer.Character.Uid == seller.Character.Uid || buyer.AccountId == seller.AccountId ||
            updatedBuyer.PlayerItems.InventoryItems.Count != buyer.PlayerItems.InventoryItems.Count + 1 ||
            updatedSeller.PlayerItems.InventoryItems.Count != seller.PlayerItems.InventoryItems.Count - 1 ||
            !ReferenceEquals(updatedBuyer.Character.Attributes, buyer.Character.Attributes) ||
            !ReferenceEquals(updatedSeller.Character.Attributes, seller.Character.Attributes) ||
            !LegacyVendorUnmovedStoresMatch(buyer.PlayerItems, updatedBuyer.PlayerItems) ||
            !LegacyVendorUnmovedStoresMatch(seller.PlayerItems, updatedSeller.PlayerItems)) return false;
        var appended = updatedBuyer.PlayerItems.InventoryItems[^1];
        if (!sellerItems.TryGetInventory(appended.Uid, out incoming) ||
            !preparedBuyerItems.TryGetInventory(appended.Uid, out var delivered) ||
            buyerItems.ContainsAny(incoming) || seller.LegacyVendorListings is not { } listings ||
            !listings.TryGetListing(appended.Uid, out var listing) ||
            !VendorWalletsMatchListing(buyer, updatedBuyer, seller, updatedSeller, listing)) return false;

        // The buyer must receive detached physical rows, including every child.
        // Otherwise an escaped seller/source row could mutate the committed buyer's item.
        var oldRows = new HashSet<PlayerItemSnapshotItem>(ReferenceEqualityComparer.Instance);
        if (!incoming.AllSourceRows(row => oldRows.Add(row)) ||
            !delivered.AllSourceRows(row => !oldRows.Contains(row)) ||
            !LegacyVendorUnmovedInventoryMatch(buyer.PlayerItems, updatedBuyer.PlayerItems,
                seller.PlayerItems, updatedSeller.PlayerItems, appended.Uid)) return false;
        var removed = listings.TryRemove(appended.Uid);
        if (!removed.Removed || !SameLegacyVendorListings(removed.Book, updatedSeller.LegacyVendorListings)) return false;

        try
        {
            var ownedBuyer = buyerItems.WithOwnedItemsForPersistence(buyer).PlayerItems;
            var ownedSeller = sellerItems.WithOwnedItemsForPersistence(seller).PlayerItems;
            var expectedBuyer = ownedBuyer with
            {
                InventoryItems = ownedBuyer.InventoryItems.Append(incoming.CreateInventoryItem()).ToArray(),
            };
            var expectedSeller = ownedSeller with
            {
                InventoryItems = ownedSeller.InventoryItems.Where(row => row.Uid != appended.Uid).ToArray(),
            };
            if (!preparedBuyerItems.HasBytes(PlayerItemSnapshotLoader.ToLegacyBytes(expectedBuyer)) ||
                !preparedSellerItems.HasBytes(PlayerItemSnapshotLoader.ToLegacyBytes(expectedSeller))) return false;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException) { return false; }
        reason = string.Empty;
        return true;
    }

    private static bool VendorWalletsMatchListing(WorldSession buyer, WorldSession updatedBuyer,
        WorldSession seller, WorldSession updatedSeller, LegacyVendorListing listing)
    {
        if (listing.Amount < 0 || listing.CostMode is not (LegacyVendorListingCostMode.Gold or LegacyVendorListingCostMode.CPs) ||
            updatedBuyer.Character.Money < 0 || updatedSeller.Character.Money < 0 ||
            updatedBuyer.Character.ConquerPoints is < 0 or > uint.MaxValue ||
            updatedSeller.Character.ConquerPoints is < 0 or > uint.MaxValue) return false;
        try
        {
            var gold = listing.CostMode == LegacyVendorListingCostMode.Gold ? listing.Amount : 0;
            var cp = listing.CostMode == LegacyVendorListingCostMode.CPs ? listing.Amount : 0;
            return updatedBuyer.Character.Money == checked(buyer.Character.Money - gold) &&
                updatedSeller.Character.Money == checked(seller.Character.Money + gold) &&
                updatedBuyer.Character.ConquerPoints == checked(buyer.Character.ConquerPoints - cp) &&
                updatedSeller.Character.ConquerPoints == checked(seller.Character.ConquerPoints + cp) &&
                updatedBuyer == (buyer with
                {
                    Character = buyer.Character with { Money = updatedBuyer.Character.Money, ConquerPoints = updatedBuyer.Character.ConquerPoints },
                    PlayerItems = updatedBuyer.PlayerItems,
                }) && updatedSeller == (seller with
                {
                    Character = seller.Character with { Money = updatedSeller.Character.Money, ConquerPoints = updatedSeller.Character.ConquerPoints },
                    PlayerItems = updatedSeller.PlayerItems, LegacyVendorListings = updatedSeller.LegacyVendorListings,
                });
        }
        catch (OverflowException) { return false; }
    }

    private static bool SameLegacyVendorListings(LegacyVendorListingBook expected, LegacyVendorListingBook? actual) =>
        actual is not null && expected.Listings.SequenceEqual(actual.Listings);

    private static bool LegacyVendorUnmovedStoresMatch(PlayerItemSnapshot before, PlayerItemSnapshot after) =>
        ReferenceEquals(before.RelicFuseItems, after.RelicFuseItems) &&
        ReferenceEquals(before.MythsoulBagItems, after.MythsoulBagItems) &&
        ReferenceEquals(before.EquipmentItems, after.EquipmentItems) &&
        ReferenceEquals(before.RuneItems, after.RuneItems) && ReferenceEquals(before.WardrobeItems, after.WardrobeItems) &&
        ReferenceEquals(before.WarehouseItems, after.WarehouseItems);

    private static bool LegacyVendorUnmovedInventoryMatch(PlayerItemSnapshot buyer, PlayerItemSnapshot updatedBuyer,
        PlayerItemSnapshot seller, PlayerItemSnapshot updatedSeller, uint transferredUid)
    {
        for (var index = 0; index < buyer.InventoryItems.Count; index++)
            if (!ReferenceEquals(buyer.InventoryItems[index], updatedBuyer.InventoryItems[index])) return false;
        var remaining = 0;
        foreach (var row in seller.InventoryItems)
            if (row.Uid != transferredUid && !ReferenceEquals(row, updatedSeller.InventoryItems[remaining++])) return false;
        return remaining == updatedSeller.InventoryItems.Count;
    }
}
