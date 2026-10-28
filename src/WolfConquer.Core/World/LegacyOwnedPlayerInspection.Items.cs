using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyOwnedPlayerInspection
{
    private static LegacyOwnedPlayerItem[] SummarizeInspectionItems(PlayerItemSnapshot snapshot, out int count)
    {
        var rows = new List<LegacyOwnedPlayerItem>();
        var total = 0;
        void Add(string name, PlayerItemSnapshotItem item)
        {
            total++;
            if (rows.Count < MaximumSummaryRows)
                rows.Add(new(name, item.Uid, item.ItemId, item.Position, item.WarehouseId, item.Plus, item.StackSize,
                    item.Durability, item.MaximumDurability, item.SocketOne, item.SocketTwo, item.Bound, item.Locked));
        }
        foreach (var group in new (string Name, IReadOnlyList<PlayerItemSnapshotItem> Rows)[]
        {
            ("inventory", snapshot.InventoryItems), ("equipment", snapshot.EquipmentItems),
            ("warehouse", snapshot.WarehouseItems), ("relicFuse", snapshot.RelicFuseItems),
            ("mythsoulBag", snapshot.MythsoulBagItems), ("rune", snapshot.RuneItems), ("wardrobe", snapshot.WardrobeItems),
        })
            foreach (var item in group.Rows)
            {
                Add(group.Name, item);
                foreach (var deposited in item.DepositedItems) Add(group.Name + ".deposited", deposited);
            }
        count = total;
        return rows.ToArray();
    }
}
