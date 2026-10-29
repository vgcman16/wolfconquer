using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// A single owner's character/items WAL transaction. Sale eligibility, one-item removal and native
// price/publication ordering belong to the caller; this is not another sale implementation.
// Nonnegative signed wallets and coherent aliases are managed safety guards, not native unchecked overflow parity.
internal static partial class LegacyNpcSalePersistence
{
    private enum PersistenceMode { Sale, ItemRemoval, FloorPickup, FloorPickupInventory }
    private enum CharacterWriteMode { SaleWallet, Preserve, PickupMoney, PickupConquerPoints }

    internal static bool TryPersist(WorldSession original, WorldSession updated, string? playerItemsRoot,
        out string reason) => TryPersist(original, updated, playerItemsRoot, null, out reason);

    internal static bool TryPersist(WorldSession original, WorldSession updated, string? playerItemsRoot,
        LegacyJournalInterruption? interruptAfterReplacement, out string reason) =>
        TryPersistCore(original, updated, playerItemsRoot, PersistenceMode.Sale, interruptAfterReplacement, out reason);

    private static bool TryPersistCore(WorldSession original, WorldSession updated, string? playerItemsRoot,
        PersistenceMode mode, LegacyJournalInterruption? interruptAfterReplacement, out string reason,
        FloorItemRuntimeEntry? pickupEntry = null, PlayerItemSnapshotItem? pickupItem = null,
        LegacyFloorPickupInventoryChange? pickupInventory = null)
    {
        reason = string.Empty;
        try
        {
            long warehouse = 0;
            if (mode == PersistenceMode.ItemRemoval)
            { if (!TryValidateItemRemovalSessions(original, updated, out reason)) return false; }
            else if (mode == PersistenceMode.FloorPickup)
            { if (!TryValidateFloorPickupSessions(original, updated, pickupEntry, pickupItem, out warehouse, out reason)) return false; }
            else if (mode == PersistenceMode.FloorPickupInventory)
            { if (!TryValidateFloorPickupInventorySessions(original, updated, pickupEntry, pickupInventory, out warehouse, out reason)) return false; }
            else if (!TryValidateSessions(original, updated, out warehouse, out reason)) return false;
            var characterMode = mode is PersistenceMode.ItemRemoval or PersistenceMode.FloorPickupInventory ? CharacterWriteMode.Preserve :
                mode != PersistenceMode.FloorPickup ? CharacterWriteMode.SaleWallet : pickupEntry!.Kind switch
                {
                    FloorItemRuntimePickupKind.Item => CharacterWriteMode.Preserve,
                    FloorItemRuntimePickupKind.Money => CharacterWriteMode.PickupMoney,
                    _ => CharacterWriteMode.PickupConquerPoints,
                };
            if (string.IsNullOrWhiteSpace(original.UsersRoot) || string.IsNullOrWhiteSpace(updated.UsersRoot) ||
                string.IsNullOrWhiteSpace(playerItemsRoot))
            { reason = "npcSaleRootMissing"; return false; }
            var usersRoot = Path.GetFullPath(original.UsersRoot);
            var itemsRoot = Path.GetFullPath(playerItemsRoot);
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            if (!comparer.Equals(usersRoot, Path.GetFullPath(updated.UsersRoot)) || !Directory.Exists(usersRoot) ||
                File.Exists(itemsRoot))
            { reason = "npcSaleRootBindingInvalid"; return false; }
            var characterPath = Path.Combine(usersRoot, original.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            var itemPath = Path.Combine(itemsRoot, original.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            EnsureSafePath(characterPath);
            EnsureSafePath(itemPath);
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked([characterPath, itemPath], () =>
            {
                if (LegacyTradeJournal.HasPending(usersRoot))
                { failure = LegacyTradeJournal.RecoveryRequired + ":pending"; return false; }
                if (!TryPrepareCharacter(characterPath, original, updated, warehouse, characterMode,
                    out var beforeCharacter, out var afterCharacter, out failure)) return false;
                if (!TryItemBytes(original.PlayerItems, out var expectedItems, out var originallyEmpty, out failure) ||
                    !TryItemBytes(updated.PlayerItems, out var afterItems, out _, out failure)) return false;
                if (mode == PersistenceMode.ItemRemoval && !TryValidateItemRemovalImage(original.PlayerItems, updated.PlayerItems,
                    afterItems, out failure)) return false;
                if (mode == PersistenceMode.FloorPickup && !TryValidateFloorPickupImage(original.PlayerItems,
                    updated.PlayerItems, pickupEntry!, pickupItem, afterItems, out failure)) return false;
                if (mode == PersistenceMode.FloorPickupInventory && !TryValidateFloorPickupInventoryImage(original.PlayerItems,
                    updated.PlayerItems, pickupInventory!, afterItems, out failure)) return false;
                var beforeItems = ReadBytes(itemPath, allowMissing: true);
                if (beforeItems is null ? !originallyEmpty : !beforeItems.AsSpan().SequenceEqual(expectedItems))
                { failure = "npcSaleItemBaselineStale"; return false; }
                var result = LegacyTradeJournal.TryCommitSingleOwner(usersRoot, itemsRoot,
                [
                    new(LegacyTradeJournalFileKind.Character, original.AccountId, beforeCharacter, afterCharacter),
                    new(LegacyTradeJournalFileKind.Items, original.Character.Uid, beforeItems, afterItems),
                ], interruptAfterReplacement, out failure);
                if (!result && !failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
                {
                    // Ordinary failure is safe only when BOTH exact preimages are still proven.
                    try
                    {
                        if (SameBytes(ReadBytes(characterPath, false), beforeCharacter) &&
                            SameBytes(ReadBytes(itemPath, true), beforeItems)) return false;
                    }
                    catch (Exception ex) when (IsFileFailure(ex)) { }
                    failure = LegacyTradeJournal.RecoveryRequired + ":npcSaleRollbackUnproven";
                }
                return result;
            });
            reason = failure;
            return saved;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { reason = "npcSalePersistence:" + ex.GetType().Name; return false; }
    }

    private static bool TryValidateSessions(WorldSession original, WorldSession updated,
        out long warehouse, out string reason)
    {
        warehouse = 0;
        reason = "npcSaleSessionBindingInvalid";
        if (original is null || updated is null || original.AccountId <= 0 ||
            original.AccountId != updated.AccountId || original.Character.Uid is <= 0 or > uint.MaxValue ||
            original.Character.Uid != updated.Character.Uid || original.LegacyTradeActive || updated.LegacyTradeActive ||
            original.Character.Money < 0 || updated.Character.Money < 0 ||
            original.Character.ConquerPoints is < 0 or > uint.MaxValue ||
            original.Character.ConquerPoints != updated.Character.ConquerPoints) return false;
        if ((original.Character with { Money = updated.Character.Money, Attributes = updated.Character.Attributes }) != updated.Character)
        { reason = "npcSaleUnrelatedCharacterMutation"; return false; }
        if (!TryFields(original.Character.Attributes, out var before, out reason) ||
            !TryFields(updated.Character.Attributes, out var after, out reason)) return false;
        if (!TryWarehouse(before, out _, out reason) || !TryWarehouse(after, out warehouse, out reason) ||
            !after.ContainsKey("WHMoney") || before.ContainsKey("WarehouseMoney") != after.ContainsKey("WarehouseMoney"))
        { if (reason.Length == 0) reason = "npcSaleWarehouseProjectionInvalid"; return false; }
        if (!LegacyTradeEscrowClaims.TryRead(before, out var beforeClaim, out reason) || beforeClaim is not null ||
            !LegacyTradeEscrowClaims.TryRead(after, out var afterClaim, out reason) || afterClaim is not null)
        { if (reason.Length == 0) reason = "npcSaleTradeEscrowActive"; return false; }
        if (!LegacySecondaryPasswordState.TryRead(before, out _, out reason) ||
            !LegacySecondaryPasswordState.TryRead(after, out _, out reason)) return false;
        foreach (var key in new[] { "Money", "WHMoney", "WarehouseMoney" })
        { before.Remove(key); after.Remove(key); }
        if (before.Count != after.Count || before.Any(pair => !after.TryGetValue(pair.Key, out var value) || value != pair.Value))
        { reason = "npcSaleUnrelatedAttributeMutation"; return false; }
        reason = string.Empty;
        return true;
    }

    private static bool TryItemBytes(PlayerItemSnapshot items, out byte[] bytes, out bool empty, out string reason)
    {
        bytes = [];
        reason = string.Empty;
        var all = items.InventoryItems.Concat(items.RelicFuseItems).Concat(items.MythsoulBagItems)
            .Concat(items.EquipmentItems).Concat(items.RuneItems).Concat(items.WardrobeItems).Concat(items.WarehouseItems).ToArray();
        empty = all.Length == 0;
        var uids = new HashSet<uint>();
        bool Valid(PlayerItemSnapshotItem item, int depth)
        {
            if (item is null || item.Uid == 0 || !uids.Add(item.Uid) || depth > 1) return false;
            return item.DepositedItems.All(child => Valid(child, depth + 1));
        }
        if (!all.All(item => Valid(item, 0)))
        { reason = "npcSaleItemIdentityAmbiguous"; return false; }
        bytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);
        return true;
    }

    private static bool SameBytes(byte[]? current, byte[]? expected) => current is null
        ? expected is null : expected is not null && current.AsSpan().SequenceEqual(expected);

    private static bool IsFileFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or OverflowException or System.Security.SecurityException;
}
