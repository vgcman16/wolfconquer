using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNpcSalePersistence
{
    internal static bool TryPersistFloorPickupInventory(WorldSession original, WorldSession updated, string? playerItemsRoot,
        FloorItemRuntimeEntry entry, LegacyFloorPickupInventoryChange inventory, out string reason) =>
        TryPersistFloorPickupInventory(original, updated, playerItemsRoot, entry, inventory, null, out reason);

    internal static bool TryPersistFloorPickupInventory(WorldSession original, WorldSession updated, string? playerItemsRoot,
        FloorItemRuntimeEntry entry, LegacyFloorPickupInventoryChange inventory,
        Func<int, bool>? interruptAfterReplacement, out string reason) =>
        TryPersistCore(original, updated, playerItemsRoot, PersistenceMode.FloorPickupInventory,
            interruptAfterReplacement, out reason, pickupEntry: entry, pickupInventory: inventory);

    private static bool TryValidateFloorPickupInventorySessions(WorldSession original, WorldSession updated,
        FloorItemRuntimeEntry? entry, LegacyFloorPickupInventoryChange? inventory, out long warehouse, out string reason)
    {
        warehouse = 0;
        reason = "floorPickupInventoryPlanBindingInvalid";
        if (entry is null || entry.Kind != FloorItemRuntimePickupKind.Item || inventory is null ||
            inventory.IncomingItem is null || inventory.Item is null || inventory.InitialSendItem is null ||
            inventory.UpdatedItems is null ||
            inventory.Catalog is null || inventory.Catalog.Id <= 0 || (uint)inventory.Catalog.Id != entry.Packet.ItemId)
            return false;
        // Reuse the unchanged strict owner/character/PIN/claim checks. Only the
        // entry's position normalization is permitted before the native Add plan.
        return TryValidateFloorPickupSessions(original, updated, entry, inventory.IncomingItem.WithPosition(0),
            out warehouse, out reason);
    }

    private static bool TryValidateFloorPickupInventoryImage(PlayerItemSnapshot original, PlayerItemSnapshot updated,
        LegacyFloorPickupInventoryChange inventory, byte[] updatedBytes, out string reason)
    {
        // Claimed final/initial images and lock notification are not authority.
        // Recompute Add/Send/SendItemLocked at its captured local timestamp.
        if (!LegacyFloorPickupInventory.TryPlan(original, inventory.IncomingItem, inventory.Catalog, inventory.Now,
            out var expected, out reason)) return false;
        if (!TryItemBytes(SinglePickupItem(expected.Item), out var expectedItem, out _, out reason) ||
            !TryItemBytes(SinglePickupItem(inventory.Item), out var actualItem, out _, out reason) ||
            !TryItemBytes(SinglePickupItem(expected.InitialSendItem), out var expectedInitial, out _, out reason) ||
            !TryItemBytes(SinglePickupItem(inventory.InitialSendItem), out var actualInitial, out _, out reason) ||
            !TryItemBytes(inventory.UpdatedItems, out var claimedBytes, out _, out reason)) return false;
        if (expected.IsMerge != inventory.IsMerge || !expectedItem.AsSpan().SequenceEqual(actualItem) ||
            !expectedInitial.AsSpan().SequenceEqual(actualInitial) || expected.UnlockDate != inventory.UnlockDate ||
            !MatchesFloorPickupInventoryImage(expected.UpdatedItems, inventory.UpdatedItems, claimedBytes) ||
            !MatchesFloorPickupInventoryImage(expected.UpdatedItems, updated, updatedBytes))
        { reason = "floorPickupInventoryPlanChanged"; return false; }
        reason = string.Empty;
        return true;
    }

    private static bool MatchesFloorPickupInventoryImage(PlayerItemSnapshot expected, PlayerItemSnapshot actual, byte[] actualBytes) =>
        SameItemCollectionUids(expected.InventoryItems, actual.InventoryItems) &&
        SameItemCollectionUids(expected.RelicFuseItems, actual.RelicFuseItems) &&
        SameItemCollectionUids(expected.MythsoulBagItems, actual.MythsoulBagItems) &&
        SameItemCollectionUids(expected.EquipmentItems, actual.EquipmentItems) &&
        SameItemCollectionUids(expected.RuneItems, actual.RuneItems) &&
        SameItemCollectionUids(expected.WardrobeItems, actual.WardrobeItems) &&
        SameItemCollectionUids(expected.WarehouseItems, actual.WarehouseItems) &&
        PlayerItemSnapshotLoader.ToLegacyBytes(expected).AsSpan().SequenceEqual(actualBytes);
}
