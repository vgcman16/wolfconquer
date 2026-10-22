using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyFloorPickupInventoryChange(PlayerItemSnapshot UpdatedItems,
    PlayerItemSnapshotItem Item, bool IsMerge, PlayerItemSnapshotItem IncomingItem, ItemType Catalog, DateTime Now,
    PlayerItemSnapshotItem InitialSendItem, LegacyItemLockPacket? UnlockDate);

internal static class LegacyFloorPickupInventory
{
    internal static bool TryPlan(PlayerItemSnapshot original, PlayerItemSnapshotItem incoming,
        ItemType catalog, DateTime now, out LegacyFloorPickupInventoryChange change, out string reason)
    {
        change = null!;
        reason = "floorPickupInventoryInputInvalid";
        if (original is null || incoming is null || catalog is null) return false;
        // FloorMap174 checks HaveSpace(1) even when Inventory.Add could merge.
        if (original.InventoryItems.Count >= 40)
        { reason = "floorPickupInventoryFull"; return false; }
        if (catalog.Id <= 0 || incoming.ItemId != (uint)catalog.Id || incoming.Uid == 0)
        { reason = "floorPickupInventoryItemBindingInvalid"; return false; }
        // Native DBItem.Convert rejects a malformed/missing uint Time. Failing
        // closed here is a catalog-admission adaptation, not a guessed duration.
        if (!catalog.TryGetLegacyTime(out var minutes))
        { reason = "floorPickupInventoryCatalogTimeInvalid"; return false; }
        // Managed ambiguity fence: native CheakUp may allocate a global UID on
        // collision. No such counter ownership is invented by this pure plan.
        if (!HasUniqueUnownedIncoming(original, incoming))
        { reason = "floorPickupInventoryUidAmbiguousOrOwned"; return false; }

        var limit = catalog.LegacyStackSize;
        var wholeStack = incoming.StackSize > 1;
        // Native ClientItems is concurrent-dictionary enumeration. This managed
        // model deliberately selects the FIRST current inventory-list match,
        // not UID order or a new Bound/Plus/metadata compatibility predicate.
        var destination = !wholeStack && limit > 0 ? original.InventoryItems.FirstOrDefault(
            item => item.ItemId == incoming.ItemId && item.StackSize < limit) : null;
        var output = destination is null ? incoming.WithPosition(0).WithStackSize(
            incoming.StackSize == 0 ? (ushort)1 : incoming.StackSize) :
            destination.WithStackSize(checked((ushort)(destination.StackSize + 1)));
        if (!wholeStack && minutes != 0 && limit == 1)
        {
            try
            {
                // Inventory1005..1040 assigns EndDate; ClientItems223 stores
                // EndDate.ToBinary, including the caller-supplied DateTime Kind.
                output = output.WithActivation(1, now.AddMinutes(minutes).ToBinary());
            }
            catch (ArgumentException)
            { reason = "floorPickupInventoryTimeOutsideRange"; return false; }
        }
        // Both Inventory.Add's merge and UpdateADD send the resulting item.
        // Apply that prefix before publication/persistence, including whole
        // incoming stacks that deliberately bypass the Add timer branch.
        if (!LegacyFloorPickupItemSend.TryProject(output, catalog, out output, out reason)) return false;
        var initialSendItem = output;
        if (!LegacyFloorPickupItemLock.TryProject(output, now, out output, out var unlockDate, out reason)) return false;
        var updated = original with
        {
            InventoryItems = destination is null ? [.. original.InventoryItems, output] :
                original.InventoryItems.Select(item => ReferenceEquals(item, destination) ? output : item).ToArray(),
        };
        change = new(updated, output, destination is not null, incoming, catalog, now, initialSendItem, unlockDate);
        reason = string.Empty;
        return true;
    }

    private static bool HasUniqueUnownedIncoming(PlayerItemSnapshot original, PlayerItemSnapshotItem incoming)
    {
        var seen = new HashSet<uint>();
        bool Add(PlayerItemSnapshotItem item, int depth)
        {
            if (item is null || item.Uid == 0 || item.DepositedItems is null || depth > 1 || !seen.Add(item.Uid)) return false;
            return item.DepositedItems.All(child => Add(child, depth + 1));
        }
        var all = original.InventoryItems.Concat(original.RelicFuseItems).Concat(original.MythsoulBagItems)
            .Concat(original.EquipmentItems).Concat(original.RuneItems).Concat(original.WardrobeItems).Concat(original.WarehouseItems);
        return all.All(item => Add(item, 0)) && Add(incoming, 0);
    }
}
