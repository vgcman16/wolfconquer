using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyDeityChestPersistence
{
    private static bool TryValidateChange(Prepared prepared, WorldSession updated,
        LegacyDragonSkinInventory after, out string reason)
    {
        reason = "deityChestChangeInvalid";
        var expected = prepared.Expected;
        if (expected.LegacyTradeActive || updated.LegacyTradeActive ||
            updated != (expected with { Character = updated.Character, PlayerItems = updated.PlayerItems }) ||
            updated.Character != (expected.Character with { ConquerPoints = updated.Character.ConquerPoints }) ||
            !ReferenceEquals(expected.Character.Attributes, updated.Character.Attributes) ||
            expected.Character.ConquerPoints is < 0 or > uint.MaxValue ||
            updated.Character.ConquerPoints != checked(expected.Character.ConquerPoints + prepared.ConquerPoints) ||
            updated.Character.ConquerPoints > uint.MaxValue || !prepared.Inventory.IsCurrent(expected.PlayerItems) ||
            !after.IsCurrent(updated.PlayerItems)) return false;
        var oldStores = LegacyDragonSkinInventory.Stores(expected.PlayerItems);
        var newStores = LegacyDragonSkinInventory.Stores(updated.PlayerItems);
        for (var index = 1; index < oldStores.Length; index++)
            if (!ReferenceEquals(oldStores[index], newStores[index])) return false;
        if (!IsExactInventoryChange(prepared, updated.PlayerItems.InventoryItems)) return false;
        reason = string.Empty;
        return true;
    }

    private static bool IsExactInventoryChange(Prepared prepared, IReadOnlyList<PlayerItemSnapshotItem> after)
    {
        var before = prepared.Expected.PlayerItems.InventoryItems;
        var keyIndex = 0;
        while (keyIndex < before.Count && (before[keyIndex].ItemId != prepared.Key || before[keyIndex].StackSize == 0)) keyIndex++;
        if (keyIndex == before.Count || before[keyIndex].DepositedItems.Count != 0) return false;
        var key = before[keyIndex];
        var removed = key.StackSize == 1;
        var grantsItem = prepared.ConquerPoints == 0;
        var countAfterDebit = before.Count - (removed ? 1 : 0);
        if (after.Count != countAfterDebit + (grantsItem ? 1 : 0) || grantsItem && after.Count > 40) return false;
        for (var index = 0; index < before.Count; index++)
        {
            if (index == keyIndex)
            {
                if (!removed && !SameRow(key.WithStackSize((ushort)(key.StackSize - 1)), after[index])) return false;
                continue;
            }
            if (!ReferenceEquals(before[index], after[index - (removed && index > keyIndex ? 1 : 0)])) return false;
        }
        if (!grantsItem) return prepared.Reward == 0 && prepared.ConquerPoints == 200_000;
        var reward = after[^1];
        return prepared.Reward != 0 && reward.ItemId == prepared.Reward && reward.DepositedItems.Count == 0 &&
            SameRow(new PlayerItemSnapshotItem { Uid = reward.Uid, ItemId = prepared.Reward,
                Durability = reward.Durability, MaximumDurability = reward.Durability, StackSize = 1 }, reward);
    }

    private static bool SameRow(PlayerItemSnapshotItem first, PlayerItemSnapshotItem second) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { first } })
            .AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { second } }));
}
