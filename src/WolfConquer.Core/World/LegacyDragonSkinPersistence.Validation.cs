using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDragonSkinPersistence
{
    private sealed record Paths(string UsersRoot, string ItemsRoot, string Character, string Items);
    private sealed record Change(LegacyDragonSkinBook BeforeBook, LegacyDragonSkinBook AfterBook,
        LegacyDragonSkinInventory BeforeItems, LegacyDragonSkinInventory AfterItems);

    private static bool TryValidate(WorldSession expected, WorldSession updated, out Change change, out string reason)
    {
        change = null!;
        reason = "dragonSkinChangeInvalid";
        if (expected is null || updated is null || expected.Character is null || updated.Character is null ||
            expected.PlayerItems is null || updated.PlayerItems is null || expected.LegacyTradeActive || updated.LegacyTradeActive ||
            updated != (expected with { Character = updated.Character, PlayerItems = updated.PlayerItems }) ||
            updated.Character != (expected.Character with { Attributes = updated.Character.Attributes })) return false;
        if (!LegacyDragonSkinBook.TryCapture(expected.Character.Attributes, out var before, out reason) ||
            !LegacyDragonSkinBook.TryCapture(updated.Character.Attributes, out var after, out reason)) return false;
        if (!LegacyDragonSkinBook.SameUnrelated(before.CopyFields(), after.CopyFields()))
        { reason = "dragonSkinUnrelatedAttributeChange"; return false; }
        var oldFields = before.CopyFields();
        if (!oldFields.TryGetValue("UID", out var text) ||
            !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) || uid != expected.Character.Uid ||
            !LegacyTradeEscrowClaims.TryRead(oldFields, out var escrow, out reason) || escrow is not null)
        { reason = "dragonSkinIdentityOrEscrowInvalid"; return false; }
        var newFields = after.CopyFields();
        if (!newFields.TryGetValue(LegacyDragonSkinBook.Key, out var canonical) || canonical != after.State.ToSnapshotValue())
        { reason = "dragonSkinCandidateStateNoncanonical"; return false; }
        if (!LegacyDragonSkinInventory.TryCapture(expected.PlayerItems, out var oldItems, out reason) ||
            !LegacyDragonSkinInventory.TryCapture(updated.PlayerItems, out var newItems, out reason)) return false;
        if (!IsExactTransition(expected.PlayerItems, updated.PlayerItems, before.State, after.State))
        { reason = "dragonSkinTransitionInvalid"; return false; }
        change = new(before, after, oldItems, newItems);
        return true;
    }

    private static bool IsExactTransition(PlayerItemSnapshot beforeItems, PlayerItemSnapshot afterItems,
        LegacyDragonSkinState before, LegacyDragonSkinState after)
    {
        var oldOwned = before.OwnedSkins;
        var newOwned = after.OwnedSkins;
        if (newOwned.Count == oldOwned.Count + 1 && before.EquippedSkin == after.EquippedSkin &&
            oldOwned.SequenceEqual(newOwned.Take(oldOwned.Count)) && TryMapped(newOwned[^1], out var unlockId))
            return IsExactDebit(beforeItems, afterItems, unlockId);
        if (oldOwned.Count != newOwned.Count + 1) return false;
        var removed = 0;
        while (removed < newOwned.Count && oldOwned[removed] == newOwned[removed]) removed++;
        var skin = oldOwned[removed];
        if (!TryMapped(skin, out var retrieveId) ||
            !oldOwned.Where((_, index) => index != removed).SequenceEqual(newOwned) ||
            after.EquippedSkin != (before.EquippedSkin == skin ? 0U : before.EquippedSkin)) return false;
        return IsExactAppend(beforeItems, afterItems, retrieveId);
    }

    private static bool IsExactDebit(PlayerItemSnapshot before, PlayerItemSnapshot after, uint itemId)
    {
        if (!SameOtherStores(before, after)) return false;
        var index = 0;
        while (index < before.InventoryItems.Count &&
            (before.InventoryItems[index].ItemId != itemId || before.InventoryItems[index].Bound != 0 ||
                before.InventoryItems[index].StackSize == 0)) index++;
        if (index == before.InventoryItems.Count) return false;
        var cost = before.InventoryItems[index];
        if (cost.DepositedItems.Count != 0) return false;
        var remove = cost.StackSize == 1;
        if (after.InventoryItems.Count != before.InventoryItems.Count - (remove ? 1 : 0)) return false;
        for (var oldIndex = 0; oldIndex < before.InventoryItems.Count; oldIndex++)
        {
            if (oldIndex == index)
            {
                if (!remove && !SameRow(cost.WithStackSize((ushort)(cost.StackSize - 1)), after.InventoryItems[index])) return false;
                continue;
            }
            var newIndex = oldIndex - (remove && oldIndex > index ? 1 : 0);
            if (!ReferenceEquals(before.InventoryItems[oldIndex], after.InventoryItems[newIndex])) return false;
        }
        return true;
    }

    private static bool IsExactAppend(PlayerItemSnapshot before, PlayerItemSnapshot after, uint itemId)
    {
        if (!SameOtherStores(before, after) || after.InventoryItems.Count != before.InventoryItems.Count + 1 ||
            after.InventoryItems.Count > 40) return false;
        for (var index = 0; index < before.InventoryItems.Count; index++)
            if (!ReferenceEquals(before.InventoryItems[index], after.InventoryItems[index])) return false;
        var added = after.InventoryItems[^1];
        return added.ItemId == itemId && added.DepositedItems.Count == 0 &&
            SameRow(new PlayerItemSnapshotItem { Uid = added.Uid, ItemId = itemId, Durability = added.Durability,
                MaximumDurability = added.Durability, StackSize = 1 }, added);
    }

    private static bool SameOtherStores(PlayerItemSnapshot first, PlayerItemSnapshot second)
    {
        var before = LegacyDragonSkinInventory.Stores(first);
        var after = LegacyDragonSkinInventory.Stores(second);
        for (var index = 1; index < before.Length; index++)
            if (!ReferenceEquals(before[index], after[index])) return false;
        return true;
    }

    private static bool SameRow(PlayerItemSnapshotItem first, PlayerItemSnapshotItem second) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [first] })
            .AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = [second] }));

    internal static bool TryMapped(uint skin, out uint itemId)
    {
        itemId = skin switch { 1 => 4_200_018, 2 => 4_200_016, 3 => 4_200_017, _ => 0 };
        return itemId != 0;
    }

    private static bool TryPaths(WorldSession expected, WorldSession updated, string? itemsRoot, out Paths paths, out string reason)
    {
        paths = null!;
        reason = "dragonSkinStorageBindingInvalid";
        if (expected.AccountId is <= 0 or > uint.MaxValue || expected.Character.Uid is <= 0 or > uint.MaxValue ||
            updated.AccountId != expected.AccountId || updated.Character.Uid != expected.Character.Uid ||
            string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(updated.UsersRoot) ||
            string.IsNullOrWhiteSpace(itemsRoot) || !Path.IsPathFullyQualified(expected.UsersRoot) ||
            !Path.IsPathFullyQualified(updated.UsersRoot) || !Path.IsPathFullyQualified(itemsRoot)) return false;
        var users = Path.GetFullPath(expected.UsersRoot);
        var items = Path.GetFullPath(itemsRoot);
        if (!SamePath(users, updated.UsersRoot) || !Directory.Exists(users) || !Directory.Exists(items)) return false;
        var name = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
        var character = Path.Combine(users, name);
        if (!BoundSource(expected.Character.SourceFile, character, name) || !BoundSource(updated.Character.SourceFile, character, name)) return false;
        paths = new(users, items, character, Path.Combine(items, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
        return true;
    }

    private static bool BoundSource(string source, string target, string name) => source == name ||
        !string.IsNullOrWhiteSpace(source) && Path.IsPathFullyQualified(source) && SamePath(source, target);

    private static bool SamePath(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
