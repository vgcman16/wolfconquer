using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyVendorListingBook
{
    private sealed record AdvertisedItem(LegacyItemSaveGraph.Closure Captured,
        WorldSessionStore.VendorItemLineage? Lineage, LegacyTradeItemMetadata? Metadata, bool DataOnly);
    private readonly Dictionary<uint, AdvertisedItem> _incarnations;

    private static bool TryCaptureDetached(PlayerItemSnapshotItem item, out LegacyItemSaveGraph.Closure captured)
    {
        captured = null!;
        // Codec provenance only. Actual bound publication requires its Store issuer.
        return LegacyItemSaveGraph.TryCapture(PlayerItemSnapshot.Empty with { InventoryItems = [item] }, out var graph) &&
            graph.TryGetInventory(item.Uid, out captured);
    }

    internal LegacyVendorListingAddResult TryAdd(LegacyItemSaveGraph.Closure captured,
        LegacyVendorListingCostMode mode, long amount, WorldSessionStore.VendorItemLineage? lineage, LegacyTradeItemMetadata? metadata) =>
        TryAddCore(captured.Source, mode, amount, captured, lineage, metadata, dataOnly: false);

    internal bool TryGetCurrentListing(uint uid, PlayerItemSnapshotItem current, WorldSessionStore store,
        ConquerTcpConnection connection, WorldSession expected, WorldDataCatalog? catalog, out LegacyVendorListing listing)
    {
        listing = default;
        if (current.Uid != uid || !_incarnations.TryGetValue(uid, out var retained) || !TryGetListing(uid, out listing)) return false;
        var actualMetadata = catalog?.TradeItemMetadata;
        // A public DATA-only book has no constructor metadata. Preserve only the
        // existing genuine unbound empty-source component path, never a native grant.
        var detachedEmpty = retained.DataOnly && retained.Lineage is null && retained.Metadata is null &&
            actualMetadata is { HasEmptySource: true } && store.IsUnboundTradeItemOwnerCurrent(connection, expected);
        if ((!ReferenceEquals(retained.Metadata, actualMetadata) && !detachedEmpty) ||
            (catalog is not null && (actualMetadata is null || !actualMetadata.IsCurrent(catalog)))) return false;
        if (actualMetadata is { HasEmptySource: false } metadata)
        {
            // Callers already own the complete current graph, including its one deposited level.
            if (!metadata.Allows(current) || !current.DepositedItems.All(metadata.Allows)) return false;
        }
        else if (!store.IsUnboundTradeItemOwnerCurrent(connection, expected)) return false;
        return retained.Lineage is { } lineage ? lineage.MatchesCurrent(store, connection, expected, current) :
            store.IsUnboundTradeItemOwnerCurrent(connection, expected) && retained.Captured.MatchesSource(current);
    }

    internal bool HasSameListingsAndIncarnations(LegacyVendorListingBook actual) =>
        Listings.SequenceEqual(actual.Listings) && _incarnations.Count == actual._incarnations.Count &&
        _incarnations.All(pair => actual._incarnations.TryGetValue(pair.Key, out var retained) && ReferenceEquals(pair.Value, retained));
}
