using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNpcSalePersistence
{
    // Native MsgItemUsuagePacket.DropItem (1361..1377) removes the entire
    // inventory UID. This journal is a managed durability adaptation, not native
    // SQL, ground-item publication, eligibility or DemonExterminator parity.
    internal static bool TryPersistItemRemoval(WorldSession original, WorldSession updated, string? playerItemsRoot,
        out string reason) => TryPersistItemRemoval(original, updated, playerItemsRoot, null, out reason);

    internal static bool TryPersistItemRemoval(WorldSession original, WorldSession updated, string? playerItemsRoot,
        LegacyJournalInterruption? interruptAfterReplacement, out string reason) =>
        TryPersistCore(original, updated, playerItemsRoot, PersistenceMode.ItemRemoval, interruptAfterReplacement, out reason);

    private static bool TryValidateItemRemovalSessions(WorldSession original, WorldSession updated, out string reason)
    {
        reason = "itemRemovalSessionBindingInvalid";
        if (original is null || updated is null || original.AccountId <= 0 ||
            original.Character.Uid is <= 0 or > uint.MaxValue || original.LegacyTradeActive || updated.LegacyTradeActive ||
            original.Character.Money < 0 || original.Character.ConquerPoints is < 0 or > uint.MaxValue) return false;
        // Only the item snapshot may change. This includes account/native owner,
        // roots, PIN/Anima state and every other session owner/projection.
        if ((original with { Character = updated.Character, PlayerItems = updated.PlayerItems }) != updated)
            return false;
        if ((original.Character with { Attributes = updated.Character.Attributes }) != updated.Character)
        { reason = "itemRemovalCharacterChanged"; return false; }
        if (!TryFields(original.Character.Attributes, out var before, out reason) ||
            !TryFields(updated.Character.Attributes, out _, out reason)) return false;
        if (!original.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).SequenceEqual(
                updated.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal)))
        { reason = "itemRemovalAttributesChanged"; return false; }
        if (!TryWarehouse(before, out _, out reason) ||
            !LegacySecondaryPasswordState.TryRead(before, out _, out reason) ||
            !LegacyTradeEscrowClaims.TryRead(before, out var claim, out reason) || claim is not null)
        { if (reason.Length == 0) reason = "itemRemovalTradeEscrowActive"; return false; }
        reason = string.Empty;
        return true;
    }

    private static bool TryValidateItemRemovalImage(PlayerItemSnapshot original, PlayerItemSnapshot updated,
        byte[] updatedBytes, out string reason)
    {
        reason = "itemRemovalMustRemoveOneWholeInventoryUid";
        if (original.InventoryItems.Count != updated.InventoryItems.Count + 1) return false;
        // Both full snapshots have already passed the shared global UID check.
        var remaining = updated.InventoryItems.Select(item => item.Uid).ToHashSet();
        var removed = original.InventoryItems.Where(item => !remaining.Contains(item.Uid)).ToArray();
        if (removed.Length != 1) return false;
        var expected = original with
        {
            InventoryItems = original.InventoryItems.Where(item => item.Uid != removed[0].Uid).ToArray(),
        };
        if (!SameItemCollectionUids(expected.InventoryItems, updated.InventoryItems) ||
            !SameItemCollectionUids(expected.RelicFuseItems, updated.RelicFuseItems) ||
            !SameItemCollectionUids(expected.MythsoulBagItems, updated.MythsoulBagItems) ||
            !SameItemCollectionUids(expected.EquipmentItems, updated.EquipmentItems) ||
            !SameItemCollectionUids(expected.RuneItems, updated.RuneItems) ||
            !SameItemCollectionUids(expected.WardrobeItems, updated.WardrobeItems) ||
            !SameItemCollectionUids(expected.WarehouseItems, updated.WarehouseItems) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(expected).AsSpan().SequenceEqual(updatedBytes)) return false;
        reason = string.Empty;
        return true;
    }

    private static bool SameItemCollectionUids(IReadOnlyList<PlayerItemSnapshotItem> expected,
        IReadOnlyList<PlayerItemSnapshotItem> actual) =>
        expected.Select(item => item.Uid).SequenceEqual(actual.Select(item => item.Uid));
}
