using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyVictoryChestPersistence
{
    private static bool TryValidateChange(Prepared before, WorldSession updated, LegacyDragonSkinInventory afterItems,
        LegacyVictoryQuestCapture afterQuests, out string reason)
    {
        reason = Prefix + "ChangeInvalid";
        var expected = before.Owner.Expected;
        if (updated is null || updated.Character is null || expected.LegacyTradeActive || updated.LegacyTradeActive ||
            updated != (expected with { Character = updated.Character, PlayerItems = updated.PlayerItems }) ||
            updated.Character != (expected.Character with { PlayerQuests = updated.Character.PlayerQuests }) ||
            !ExactItems(expected.PlayerItems, updated.PlayerItems) ||
            !ExactQuest(expected.Character.PlayerQuests, updated.Character.PlayerQuests) ||
            !before.Quests.IsCurrent(expected.Character.PlayerQuests) || !afterQuests.IsCurrent(updated.Character.PlayerQuests) ||
            !before.Inventory.IsCurrent(expected.PlayerItems) || !afterItems.IsCurrent(updated.PlayerItems)) return false;
        reason = string.Empty;
        return true;
    }

    private static bool ExactItems(PlayerItemSnapshot before, PlayerItemSnapshot after)
    {
        var oldStores = LegacyDragonSkinInventory.Stores(before);
        var newStores = LegacyDragonSkinInventory.Stores(after);
        for (var index = 1; index < oldStores.Length; index++)
            if (!ReferenceEquals(oldStores[index], newStores[index])) return false;
        if (after.InventoryItems.Count != before.InventoryItems.Count + 1 || after.InventoryItems.Count > 40) return false;
        for (var index = 0; index < before.InventoryItems.Count; index++)
            if (!ReferenceEquals(before.InventoryItems[index], after.InventoryItems[index])) return false;
        var reward = after.InventoryItems[^1];
        // Exact existing payout family/default new-row metadata, not new economics.
        if (reward.ItemId is not (3_008_727 or 3_009_002 or 4_200_004) || reward.DepositedItems.Count != 0) return false;
        var ordinary = new PlayerItemSnapshotItem { Uid = reward.Uid, ItemId = reward.ItemId,
            Durability = reward.Durability, MaximumDurability = reward.Durability, StackSize = 1 };
        return PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty with { InventoryItems = new[] { reward } })
            .AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(
                PlayerItemSnapshot.Empty with { InventoryItems = new[] { ordinary } }));
    }

    private static bool ExactQuest(PlayerQuestSnapshot before, PlayerQuestSnapshot after)
    {
        if (before.Quests.Count != after.Quests.Count) return false;
        var found = false;
        for (var index = 0; index < before.Quests.Count; index++)
        {
            var old = before.Quests[index];
            var next = after.Quests[index];
            if (old.Uid != 3_944)
            { if (!ReferenceEquals(old, next)) return false; continue; }
            if (found || old.Status != PlayerQuestStatus.Accepted || old.Intentions.Count == 0 || old.Intentions[0] < 100 ||
                next.Uid != old.Uid || next.Status != PlayerQuestStatus.Finished || next.Time != old.Time ||
                !old.Intentions.SequenceEqual(next.Intentions)) return false;
            found = true;
        }
        return found;
    }
}
