namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemLoadOwners
{
    private sealed class ItemState
    {
        internal readonly LegacyNativeGameItemRuntime Item;
        internal readonly uint Uid, Id, Warehouse;
        internal readonly ushort Position;
        internal Destination Kinds;
        internal readonly MapWatch<LegacyNativeGameItemRuntime> Deposits;
        internal ItemState(LegacyNativeGameItemRuntime item, Destination kind, MapWatch<LegacyNativeGameItemRuntime> deposits)
        {
            Item = item; Uid = item.Uid; Id = item.ITEM_ID; Warehouse = item.WH_ID; Position = item.Position; Kinds = kind; Deposits = deposits;
        }
        internal bool IsCurrent() => Item.Uid == Uid && Item.ITEM_ID == Id && Item.WH_ID == Warehouse &&
            Item.Position == Position && ReferenceEquals(Item.Deposite, Deposits.Map);
    }

    private readonly Dictionary<uint, ItemState> existingItems = new(), newItems = new();
    private readonly HashSet<LegacyNativeGameItemRuntime> physicalItems = new(ReferenceEqualityComparer.Instance);
    private readonly List<ItemState> states = new();

    private ItemState Admit(Dictionary<uint, ItemState> ledger, LegacyNativeGameItemRuntime item, Destination kind, bool child = false)
    {
        if (item is null || item.Uid == 0) throw Invalid("null or zero item identity");
        if (ledger.TryGetValue(item.Uid, out var prior))
        {
            var pair = prior.Kinds | kind;
            if (child || !ReferenceEquals(prior.Item, item) || (prior.Kinds & kind) != 0 ||
                (pair != (Destination.Equipment | Destination.WardrobeGarment) && pair != (Destination.Equipment | Destination.WardrobeMount)) ||
                item.WH_ID != 100 || item.Position is 0 or > 59) throw Invalid("duplicate UID ownership");
            prior.Kinds = pair; return prior;
        }
        if (!physicalItems.Add(item)) throw Invalid("rekeyed physical item");
        var deposits = Watch(item.Deposite);
        var result = new ItemState(item, kind, deposits); ledger.Add(result.Uid, result); states.Add(result);
        return result;
    }

    private void CaptureExistingItems()
    {
        void Map(MapWatch<LegacyNativeGameItemRuntime> map, Destination kind, uint bank = 0)
        {
            foreach (var row in map.Rows)
            {
                if (row.Value is null || row.Key != row.Value.Uid) throw Invalid("noncanonical map key");
                if (kind == Destination.Warehouse && row.Value.WH_ID != bank) throw Invalid("warehouse location mismatch");
                if (kind is Destination.WardrobeGarment or Destination.WardrobeMount) ValidateWardrobe(row.Value, kind);
                Admit(existingItems, row.Value, kind);
            }
        }
        Map(inventoryMap, Destination.Inventory); Map(equipmentMap, Destination.Equipment);
        foreach (var bank in bankRows) Map(bank.Value, Destination.Warehouse, bank.Key);
        Map(wardrobeRows[0], Destination.WardrobeGarment); Map(wardrobeRows[1], Destination.WardrobeMount);
        foreach (var item in runeState.Rows) Admit(existingItems, item, Destination.Rune);
        foreach (var row in relicState.Rows) Admit(existingItems, row.Value, Destination.Relic);
        foreach (var row in gearState.Rows) Admit(existingItems, row.Value, Destination.Gear);
        CaptureChildren(existingItems, existingItems.Values.ToArray());
    }

    private void CaptureChildren(Dictionary<uint, ItemState> ledger, ItemState[] roots)
    {
        foreach (var root in roots)
            foreach (var row in root.Deposits.Rows)
            {
                if (row.Value is null || row.Key != row.Value.Uid || ledger.ContainsKey(row.Key) ||
                    (ReferenceEquals(ledger, newItems) && existingItems.ContainsKey(row.Key))) throw Invalid("duplicate deposit UID");
                var child = Admit(ledger, row.Value, Destination.None, child: true);
                if (child.Deposits.Rows.Length != 0) throw Invalid("nested deposits");
            }
    }

    private static void ValidateWardrobe(LegacyNativeGameItemRuntime item, Destination kind)
    {
        ushort expected = kind == Destination.WardrobeGarment ? (ushort)9 : (ushort)17;
        if (item.WH_ID != 100 || LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != expected)
            throw Invalid("wardrobe bucket mismatch");
    }

    private static void ValidatePlacement(Placement row)
    {
        var item = row.Item ?? throw Invalid("missing incoming item");
        bool valid = row.Destination switch
        {
            Destination.Inventory => item.WH_ID == 0 && item.Position == 0,
            Destination.Equipment => item.WH_ID == 0 && item.Position is > 0 and <= 63 || item.WH_ID == 100 && item.Position is > 0 and <= 59,
            Destination.Warehouse => row.WarehouseId != 0 && row.WarehouseId != 100 && item.WH_ID == row.WarehouseId,
            Destination.WardrobeGarment or Destination.WardrobeMount => item.WH_ID == 100,
            Destination.Rune => item.WH_ID == 0 && (item.Position is 211 or 212 or >= 101 and <= 135),
            Destination.Relic => item.WH_ID == 0 && item.Position is >= 213 and <= 215,
            Destination.Gear => item.WH_ID == 0 && item.Position == LegacyNativeGameItemRuntime.MythsoulBag,
            _ => false
        };
        if (!valid) throw Invalid("incoming route mismatch");
        if (row.Destination is Destination.WardrobeGarment or Destination.WardrobeMount) ValidateWardrobe(item, row.Destination);
    }

    private void CaptureIncoming(Placement[] rows)
    {
        foreach (var row in rows)
        {
            int destination = (int)row.Destination;
            if (destination <= 0 || destination > 128 || (destination & (destination - 1)) != 0 ||
                (required & row.Destination) != row.Destination) throw Invalid("unrequested route");
            ValidatePlacement(row);
            if (existingItems.ContainsKey(row.Item.Uid)) throw Invalid("incoming UID collides with existing ownership");
            Admit(newItems, row.Item, row.Destination);
        }
        foreach (var item in newItems.Values)
            if ((item.Kinds & Destination.Equipment) != 0 && item.Warehouse == 100 &&
                (item.Kinds != (Destination.Equipment | Destination.WardrobeGarment) && item.Kinds != (Destination.Equipment | Destination.WardrobeMount)))
                throw Invalid("missing wardrobe equipment alias");
        CaptureChildren(newItems, newItems.Values.ToArray());
    }
}
