using System.Collections.Concurrent;
using ItemMap = System.Collections.Concurrent.ConcurrentDictionary<uint, WolfConquer.Core.World.LegacyNativeGameItemRuntime>;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemLoadOwners
{
    // Preallocate the real enumerator during capture. Final checks do not allocate,
    // hash/look up keys or acquire the ConcurrentDictionary Count locks.
    private sealed class MapWatch<T> where T : class
    {
        internal readonly ConcurrentDictionary<uint, T> Map;
        internal readonly KeyValuePair<uint, T>[] Rows;
        private readonly IEnumerator<KeyValuePair<uint, T>> reader;
        internal MapWatch(ConcurrentDictionary<uint, T> map)
        {
            Map = map; Rows = map.ToArray(); reader = map.GetEnumerator();
        }
        internal bool IsCurrent()
        {
            reader.Reset(); int index = 0;
            while (reader.MoveNext())
            {
                var row = reader.Current;
                if (index >= Rows.Length || Rows[index].Key != row.Key || !ReferenceEquals(Rows[index].Value, row.Value)) return false;
                index++;
            }
            return index == Rows.Length;
        }
    }

    private MapWatch<LegacyNativeGameItemRuntime> inventoryMap = null!, equipmentMap = null!;
    private MapWatch<ItemMap> banks = null!;
    private ItemMap[] wardrobeMaps = null!;
    private readonly List<MapWatch<LegacyNativeGameItemRuntime>> maps = new();
    private readonly HashSet<object> physicalMaps = new(ReferenceEqualityComparer.Instance);
    private MapWatch<LegacyNativeGameItemRuntime>[] wardrobeRows = null!;
    private readonly Dictionary<uint, MapWatch<LegacyNativeGameItemRuntime>> bankRows = new();
    private ItemMap? preparedInventory, preparedEquipment;
    private ConcurrentDictionary<uint, ItemMap>? preparedBanks;
    private ItemMap[]? preparedWardrobe;

    private MapWatch<LegacyNativeGameItemRuntime> Watch(ItemMap map)
    {
        if (map is null || !physicalMaps.Add(map)) throw Invalid("missing or shared physical item map");
        var result = new MapWatch<LegacyNativeGameItemRuntime>(map); maps.Add(result); return result;
    }

    private void CaptureMaps()
    {
        inventoryMap = Watch(inventory.ClientItems); equipmentMap = Watch(equipment.ClientItems);
        banks = new(warehouse.ClientItems ?? throw Invalid("missing banks"));
        foreach (var bank in banks.Rows)
        {
            if (bank.Value is null) throw Invalid("null bank");
            bankRows.Add(bank.Key, Watch(bank.Value));
        }
        wardrobeMaps = wardrobe.Items ?? throw Invalid("wardrobe maps");
        if (wardrobeMaps.Length != 2) throw Invalid("wardrobe buckets");
        wardrobeRows = new[] { Watch(wardrobeMaps[0]), Watch(wardrobeMaps[1]) };
    }

    private static ItemMap Append(MapWatch<LegacyNativeGameItemRuntime> before, Placement[] rows)
    {
        var image = new ItemMap(before.Map.Comparer);
        foreach (var row in before.Rows)
            if (!image.TryAdd(row.Key, row.Value)) throw Invalid("comparer rejected existing identity");
        foreach (var row in rows)
            if (!image.TryAdd(row.Item.Uid, row.Item)) throw Invalid("duplicate incoming key");
        return image;
    }

    private void PrepareMaps(Placement[] placements)
    {
        Placement[] For(Destination kind) => placements.Where(row => row.Destination == kind).ToArray();
        var added = For(Destination.Inventory); if (added.Length > 0) preparedInventory = Append(inventoryMap, added);
        added = For(Destination.Equipment); if (added.Length > 0) preparedEquipment = Append(equipmentMap, added);
        var garments = For(Destination.WardrobeGarment); var mounts = For(Destination.WardrobeMount);
        if (garments.Length + mounts.Length > 0)
        {
            preparedWardrobe = (ItemMap[])wardrobeMaps.Clone();
            if (garments.Length > 0) preparedWardrobe[0] = Append(wardrobeRows[0], garments);
            if (mounts.Length > 0) preparedWardrobe[1] = Append(wardrobeRows[1], mounts);
        }
        var warehouseRows = For(Destination.Warehouse);
        if (warehouseRows.Length == 0) return;
        var image = new ConcurrentDictionary<uint, ItemMap>(banks.Map.Comparer);
        foreach (var row in banks.Rows) if (!image.TryAdd(row.Key, row.Value)) throw Invalid("duplicate existing bank");
        foreach (var group in warehouseRows.GroupBy(row => row.WarehouseId))
        {
            ItemMap bank;
            if (bankRows.TryGetValue(group.Key, out var existing)) bank = Append(existing, group.ToArray());
            else
            {
                bank = new ItemMap();
                foreach (var row in group) if (!bank.TryAdd(row.Item.Uid, row.Item)) throw Invalid("duplicate bank item");
            }
            if (existing is not null)
            {
                if (!image.TryUpdate(group.Key, bank, existing.Map)) throw Invalid("bank comparison changed");
            }
            else if (!image.TryAdd(group.Key, bank)) throw Invalid("bank comparer collided with another identity");
        }
        preparedBanks = image;
    }

    private bool MapsCurrent()
    {
        if (!ReferenceEquals(inventory.ClientItems, inventoryMap.Map) || !ReferenceEquals(equipment.ClientItems, equipmentMap.Map) ||
            !ReferenceEquals(warehouse.ClientItems, banks.Map) || !ReferenceEquals(wardrobe.Items, wardrobeMaps) ||
            wardrobeMaps.Length != 2 || !ReferenceEquals(wardrobeMaps[0], wardrobeRows[0].Map) ||
            !ReferenceEquals(wardrobeMaps[1], wardrobeRows[1].Map) || !banks.IsCurrent()) return false;
        foreach (var map in maps) if (!map.IsCurrent()) return false;
        return true;
    }
}
