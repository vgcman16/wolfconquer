using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNpcSalePersistence
{
    internal static bool TryPersistFloorPickup(WorldSession original, WorldSession updated, string? playerItemsRoot,
        FloorItemRuntimeEntry entry, PlayerItemSnapshotItem? pickupItem, out string reason) =>
        TryPersistFloorPickup(original, updated, playerItemsRoot, entry, pickupItem, null, out reason);

    internal static bool TryPersistFloorPickup(WorldSession original, WorldSession updated, string? playerItemsRoot,
        FloorItemRuntimeEntry entry, PlayerItemSnapshotItem? pickupItem, Func<int, bool>? interruptAfterReplacement,
        out string reason) => TryPersistCore(original, updated, playerItemsRoot, PersistenceMode.FloorPickup,
        interruptAfterReplacement, out reason, entry, pickupItem);

    private static bool TryValidateFloorPickupSessions(WorldSession original, WorldSession updated,
        FloorItemRuntimeEntry? entry, PlayerItemSnapshotItem? pickupItem, out long warehouse, out string reason)
    {
        warehouse = 0;
        reason = "floorPickupSessionBindingInvalid";
        if (original is null || updated is null || entry is null || entry.Effect is not null || original.AccountId <= 0 ||
            original.Character.Uid is <= 0 or > uint.MaxValue || original.LegacyTradeActive || updated.LegacyTradeActive ||
            original.Character.Money < 0 || original.Character.ConquerPoints is < 0 or > uint.MaxValue ||
            (original with { Character = updated.Character, PlayerItems = updated.PlayerItems }) != updated) return false;
        if (!TryFields(original.Character.Attributes, out var before, out reason) ||
            !TryFields(updated.Character.Attributes, out _, out reason) || !TryWarehouse(before, out _, out reason)) return false;
        if (!LegacySecondaryPasswordState.TryRead(before, out _, out reason) ||
            !LegacyTradeEscrowClaims.TryRead(before, out var claim, out reason) || claim is not null)
        { if (reason.Length == 0) reason = "floorPickupTradeEscrowActive"; return false; }
        var expected = original.Character;
        if (entry.Kind == FloorItemRuntimePickupKind.Item)
        {
            if (pickupItem is null || pickupItem.Uid == 0 || pickupItem.Position != 0 ||
                pickupItem.ItemId != entry.Packet.ItemId)
            { reason = "floorPickupItemBindingInvalid"; return false; }
            if (entry.Item is { } groundItem)
            {
                if (!TryItemBytes(SinglePickupItem(groundItem.WithPosition(0)), out var groundBytes, out _, out reason) ||
                    !TryItemBytes(SinglePickupItem(pickupItem), out var pickupBytes, out _, out reason)) return false;
                if (!groundBytes.AsSpan().SequenceEqual(pickupBytes))
                { reason = "floorPickupItemImageChanged"; return false; }
            }
        }
        else
        {
            if (pickupItem is not null || entry.Item is not null)
            { reason = "floorPickupCurrencyItemUnexpected"; return false; }
            if (!LegacyFloorPickupWallet.TryAdd(original.Character, entry.Kind, entry.Amount, out var change, out reason)) return false;
            expected = change.UpdatedCharacter;
        }
        if ((expected with { Attributes = updated.Character.Attributes }) != updated.Character ||
            !expected.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(
                updated.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal)))
        { reason = "floorPickupUnrelatedCharacterMutation"; return false; }
        if (!TryWarehouse(updated.Character.Attributes, out warehouse, out reason)) return false;
        reason = string.Empty;
        return true;
    }

    private static bool TryValidateFloorPickupImage(PlayerItemSnapshot original, PlayerItemSnapshot updated,
        FloorItemRuntimeEntry entry, PlayerItemSnapshotItem? pickupItem, byte[] updatedBytes, out string reason)
    {
        var expected = entry.Kind == FloorItemRuntimePickupKind.Item ? original with
        { InventoryItems = [.. original.InventoryItems, pickupItem!] } : original;
        if (!SameItemCollectionUids(expected.InventoryItems, updated.InventoryItems) ||
            !SameItemCollectionUids(expected.RelicFuseItems, updated.RelicFuseItems) ||
            !SameItemCollectionUids(expected.MythsoulBagItems, updated.MythsoulBagItems) ||
            !SameItemCollectionUids(expected.EquipmentItems, updated.EquipmentItems) ||
            !SameItemCollectionUids(expected.RuneItems, updated.RuneItems) ||
            !SameItemCollectionUids(expected.WardrobeItems, updated.WardrobeItems) ||
            !SameItemCollectionUids(expected.WarehouseItems, updated.WarehouseItems) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(expected).AsSpan().SequenceEqual(updatedBytes))
        { reason = "floorPickupItemMutationInvalid"; return false; }
        reason = string.Empty;
        return true;
    }

    private static PlayerItemSnapshot SinglePickupItem(PlayerItemSnapshotItem item) => new([item], [], [], [], [], []);
}
