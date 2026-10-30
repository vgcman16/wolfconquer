using System.Numerics;
using ItemMap = System.Collections.Concurrent.ConcurrentDictionary<uint, WolfConquer.Core.World.LegacyNativeGameItemRuntime>;
using BanksMap = System.Collections.Concurrent.ConcurrentDictionary<uint, System.Collections.Concurrent.ConcurrentDictionary<uint, WolfConquer.Core.World.LegacyNativeGameItemRuntime>>;
using D = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeOwnedItemGraph
{
    private PhysicalState CaptureState(LegacyOwnedNativeItemImage? image) =>
        new(Inventory.ClientItems, Equipment.ClientItems, Warehouse.ClientItems, Wardrobe.Items,
            Rune.CaptureJoinedItems(this), Relic.CaptureJoinedItems(this), Gear.CaptureJoinedItems(this), image);

    private PhysicalState PrepareState(LegacyOwnedNativeItemImage image)
    {
        ValidateImageMembership(image);
        var inventory = new ItemMap(Inventory.ClientItems.Comparer);
        var equipment = new ItemMap(Equipment.ClientItems.Comparer);
        var banks = new BanksMap(Warehouse.ClientItems.Comparer);
        var wardrobe = new[]
        {
            new ItemMap(Wardrobe.Items[0].Comparer), new ItemMap(Wardrobe.Items[1].Comparer)
        };
        var runes = new List<LegacyNativeGameItemRuntime>();
        var relic = new List<KeyValuePair<uint, LegacyNativeGameItemRuntime>>();
        var gear = new List<KeyValuePair<uint, LegacyNativeGameItemRuntime>>();
        foreach (var row in image.Placements)
        {
            var item = row.Item;
            switch (row.Destination)
            {
                case D.Inventory:
                    Add(inventory, item);
                    break;
                case D.Equipment:
                    Add(equipment, item);
                    break;
                case D.WardrobeGarment:
                    Add(wardrobe[0], item);
                    break;
                case D.WardrobeMount:
                    Add(wardrobe[1], item);
                    break;
                case D.Warehouse:
                    if (!banks.TryGetValue(row.WarehouseId, out var bank))
                    {
                        bank = new ItemMap();
                        if (!banks.TryAdd(row.WarehouseId, bank))
                            throw Invalid();
                    }
                    Add(bank, item);
                    break;
                case D.Rune:
                    runes.Add(item);
                    break;
                case D.Relic:
                    relic.Add(new(item.Uid, item));
                    break;
                case D.Gear:
                    gear.Add(new(item.Uid, item));
                    break;
                default:
                    throw Invalid();
            }
        }
        return new(inventory, equipment, banks, wardrobe,
            Rune.PrepareJoinedItems(this, runes.ToArray()),
            Relic.PrepareJoinedItems(this, relic.ToArray()),
            Gear.PrepareJoinedItems(this, gear.ToArray()), image);
    }

    private static void Add(ItemMap map, LegacyNativeGameItemRuntime item)
    {
        if (!map.TryAdd(item.Uid, item))
            throw Invalid();
    }

    private static void ValidateImageMembership(LegacyOwnedNativeItemImage image)
    {
        var roots = new Dictionary<uint, (LegacyNativeGameItemRuntime Item, D Kinds)>();
        foreach (var row in image.Placements)
        {
            var item = row.Item;
            int kind = (int)row.Destination;
            if (item is null || item.Uid == 0 || kind <= 0 || kind > 128 || BitOperations.PopCount((uint)kind) != 1)
                throw Invalid();
            if (roots.TryGetValue(item.Uid, out var old))
            {
                D pair = old.Kinds | row.Destination;
                if (!ReferenceEquals(old.Item, item) ||
                    (old.Kinds & row.Destination) != 0 ||
                    (pair != (D.Equipment | D.WardrobeGarment) &&
                    pair != (D.Equipment | D.WardrobeMount)) ||
                    item.WH_ID != 100 ||
                    item.Position is 0 or > 59)
                    throw Invalid();
                roots[item.Uid] = (item, pair);
            }
            else
                roots.Add(item.Uid, (item, row.Destination));
            if (row.Destination is D.WardrobeGarment or D.WardrobeMount)
            {
                ushort expected = row.Destination == D.WardrobeGarment ? (ushort)9 : (ushort)17;
                if (item.WH_ID != 100 || LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != expected)
                    throw Invalid();
            }
            if (row.Destination == D.Warehouse &&
                (row.WarehouseId == 0 ||
                row.WarehouseId == 100 ||
                item.WH_ID != row.WarehouseId))
                throw Invalid();
        }
        var seen = new Dictionary<uint, LegacyNativeGameItemRuntime>();
        int rootCount = 0;
        foreach (var node in image.Roots)
        {
            if (!roots.TryGetValue(node.Item.Uid, out var placed) || !ReferenceEquals(placed.Item, node.Item))
                throw Invalid();
            AddTree(node.Item, seen);
            rootCount++;
        }
        if (rootCount != roots.Count)
            throw Invalid();
    }

    private static void AddTree(LegacyNativeGameItemRuntime item, Dictionary<uint, LegacyNativeGameItemRuntime> seen)
    {
        if (item is null || item.Uid == 0 || !seen.TryAdd(item.Uid, item) || item.Deposite is null)
            throw Invalid();
        foreach (var row in item.Deposite)
        {
            if (row.Key != row.Value.Uid)
                throw Invalid();
            AddTree(row.Value, seen);
        }
    }

}
