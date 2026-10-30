using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateOwnedStartupVendorImages(LegacyTradeJournalChange[] characters,
        LegacyTradeJournalChange[] items, LegacyTradeEscrowClaims.JournalWallet[] before,
        LegacyTradeEscrowClaims.JournalWallet[] after)
    {
        if (before.Any(wallet => wallet.Claim is not null) || after.Any(wallet => wallet.Claim is not null))
            throw new InvalidDataException("Owned vendor settlement cannot consume or retain trade escrow.");
        for (var index = 0; index < characters.Length; index++)
            if (!LegacyVendorPurchasePersistence.ProjectVendorCharacterImage(characters[index].Before!,
                    after[index].Money, after[index].ConquerPoints).AsSpan().SequenceEqual(characters[index].After))
                throw new InvalidDataException("Owned vendor settlement changes unrelated Character state.");
        ValidateOwnedVendorWallets(before, after);
        ValidateOwnedVendorItems(items);
    }

    private static void ValidateOwnedVendorWallets(LegacyTradeEscrowClaims.JournalWallet[] before,
        LegacyTradeEscrowClaims.JournalWallet[] after)
    {
        // The configured recorded intent is trusted. These exact deltas derive
        // settlement amount/mode; v1 does not independently attest a listing.
        var gold = checked(before[0].Money - after[0].Money);
        var cp = checked(before[0].ConquerPoints - after[0].ConquerPoints);
        if (gold < 0 || cp < 0 || gold != 0 && cp != 0 ||
            checked(before[1].Money + gold) != after[1].Money ||
            checked(before[1].ConquerPoints + cp) != after[1].ConquerPoints)
            throw new InvalidDataException("Owned vendor wallets do not express one exact buyer-to-seller payment.");
        // Both zero is a real zero-price sale; its original currency is not
        // distinguishable from the existing claim-free gift shape in v1.
    }

    private static void ValidateOwnedVendorItems(LegacyTradeJournalChange[] changes)
    {
        var buyer = PlayerItemSnapshotLoader.Parse(changes[0].Before!);
        var seller = PlayerItemSnapshotLoader.Parse(changes[1].Before!);
        var deliveredBuyer = PlayerItemSnapshotLoader.Parse(changes[0].After);
        var remainingSeller = PlayerItemSnapshotLoader.Parse(changes[1].After);
        if (!LegacyItemSaveGraph.TryCapture(buyer, out var buyerGraph) || !buyerGraph.HasBytes(changes[0].Before!) ||
            !LegacyItemSaveGraph.TryCapture(seller, out var sellerGraph) || !sellerGraph.HasBytes(changes[1].Before!) ||
            !LegacyItemSaveGraph.TryCapture(deliveredBuyer, out var deliveredGraph) || !deliveredGraph.HasBytes(changes[0].After) ||
            !LegacyItemSaveGraph.TryCapture(remainingSeller, out var remainingGraph) || !remainingGraph.HasBytes(changes[1].After) ||
            buyer.InventoryItems.Count >= OwnedTradeInventorySlots ||
            deliveredBuyer.InventoryItems.Count != buyer.InventoryItems.Count + 1 ||
            remainingSeller.InventoryItems.Count != seller.InventoryItems.Count - 1)
            throw new InvalidDataException("Owned vendor Items are not a complete lossless single-root purchase.");
        var incomingUid = deliveredBuyer.InventoryItems[^1].Uid;
        if (!sellerGraph.TryGetInventory(incomingUid, out var incoming) || buyerGraph.ContainsAny(incoming) ||
            !incoming.AllSourceRows(row => row.Bound == 0 && row.Inscribed != 1 && row.Locked == 0 &&
                !LegacyNativeItemRestrictions.IsUnableTradeItem(row.ItemId)))
            throw new InvalidDataException("Owned vendor item closure is not a tradable seller-owned inventory root.");
        var expectedBuyer = buyer with { InventoryItems = buyer.InventoryItems.Append(incoming.CreateInventoryItem()).ToArray() };
        var expectedSeller = seller with { InventoryItems = seller.InventoryItems.Where(row => row.Uid != incomingUid).ToArray() };
        if (!deliveredGraph.HasBytes(PlayerItemSnapshotLoader.ToLegacyBytes(expectedBuyer)) ||
            !remainingGraph.HasBytes(PlayerItemSnapshotLoader.ToLegacyBytes(expectedSeller)))
            throw new InvalidDataException("Owned vendor settlement changes another item, container or closure.");
    }
}
