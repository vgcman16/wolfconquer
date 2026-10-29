using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyRuntimeInspectionProjection
{
    internal sealed record Row(LegacyNativeGameItemRuntime Source, LegacyOwnedPlayerItem Value);
    internal sealed record ManagedRow(PlayerItemSnapshotItem Source, LegacyOwnedPlayerItem Value);
    internal sealed record ManagedRows(IReadOnlyList<PlayerItemSnapshotItem> Inventory,
        IReadOnlyList<PlayerItemSnapshotItem> Equipment, ManagedRow[] Rows);
    internal sealed record Native(LegacyNativeInventoryRuntime? Inventory, LegacyNativeEquipItemStorage? Equipment,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>? InventoryMap,
        ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>? EquipmentMap,
        LegacyRuntimeNativeVitals Vitals, Row[] Rows, WardrobeAlias[] Aliases, string? UnavailableReason = null);

    internal static ManagedRows CaptureManaged(WorldSession session)
    {
        var items = session.PlayerItems ?? throw Invalid();
        var inventory = items.InventoryItems; var equipment = items.EquipmentItems;
        if (inventory is null || equipment is null || inventory.Count + (long)equipment.Count > LegacyRuntimeInspectionSnapshot.MaximumRows)
            throw new InvalidDataException("runtimeInspectionRowsUnavailableOrBoundExceeded");
        var seen = new HashSet<uint>(); var slots = new HashSet<ushort>();
        ManagedRow Copy(PlayerItemSnapshotItem row, string container)
        {
            if (row is null || row.Uid == 0 || row.ItemId == 0 || !seen.Add(row.Uid)) throw Invalid();
            RequireSelectedPlacement(container, row.ItemId, row.Position, row.WarehouseId, slots);
            return new(row, new(container, row.Uid, row.ItemId, row.Position, row.WarehouseId, row.Plus, row.StackSize,
                row.Durability, row.MaximumDurability, row.SocketOne, row.SocketTwo, row.Bound, row.Locked));
        }
        var inventoryCount = inventory.Count; var equipmentCount = equipment.Count;
        if (inventoryCount < 0 || equipmentCount < 0 || inventoryCount + (long)equipmentCount > LegacyRuntimeInspectionSnapshot.MaximumRows) throw Invalid();
        var result = new List<ManagedRow>();
        for (var index = 0; index < inventoryCount; index++) result.Add(Copy(inventory[index], "inventory"));
        for (var index = 0; index < equipmentCount; index++) result.Add(Copy(equipment[index], "equipment"));
        if (inventory.Count != inventoryCount || equipment.Count != equipmentCount) throw Invalid();
        return new(inventory, equipment, result.OrderBy(row => row.Value.Uid).ToArray());
    }

    internal static bool SameManaged(ManagedRows first, ManagedRows second) =>
        ReferenceEquals(first.Inventory, second.Inventory) && ReferenceEquals(first.Equipment, second.Equipment) &&
        first.Rows.Length == second.Rows.Length && first.Rows.Zip(second.Rows).All(pair =>
            ReferenceEquals(pair.First.Source, pair.Second.Source) && pair.First.Value == pair.Second.Value);

    internal static Native CaptureNative(LegacyOrdinarySpellBinding binding)
    {
        var client = binding.Client; var player = binding.Player;
        var inventory = client.Inventory; var equipment = client.Equipment;
        if (inventory is not null && !ReferenceEquals(inventory.Owner, client) ||
            equipment is not null && !equipment.IsOwnedBy(client)) throw Invalid();
        var unavailable = inventory is not null && equipment is not null ? null :
            inventory is null && equipment is null ? "nativeInventoryAndEquipmentNotInitialized" :
            inventory is null ? "nativeInventoryNotInitialized" : "nativeEquipmentNotInitialized";
        // Absence of one owner never exempts a present selected graph from
        // validation or repeated witnesses. Incomplete rows are not published.
        var inv = inventory is null ? null : inventory.ClientItems ?? throw Invalid();
        var equip = equipment is null ? null : equipment.ClientItems ?? throw Invalid();
        if (inv is not null && !ReferenceEquals(inv.Comparer, EqualityComparer<uint>.Default) ||
            equip is not null && !ReferenceEquals(equip.Comparer, EqualityComparer<uint>.Default) ||
            inv is not null && ReferenceEquals(inv, equip) ||
            (inv?.Count ?? 0) + (long)(equip?.Count ?? 0) > LegacyRuntimeInspectionSnapshot.MaximumRows) throw Invalid();
        var seen = new HashSet<uint>(); var slots = new HashSet<ushort>();
        var rows = new List<Row>(); var aliases = new List<WardrobeAlias>();
        void Copy(ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> map, string container)
        {
            foreach (var pair in map)
            {
                if (rows.Count >= LegacyRuntimeInspectionSnapshot.MaximumRows) throw Invalid();
                var item = pair.Value;
                if (item is null || pair.Key == 0 || pair.Key != item.Uid || item.ITEM_ID == 0 || !seen.Add(pair.Key)) throw Invalid();
                RequireSelectedPlacement(container, item.ITEM_ID, item.Position, item.WH_ID, slots);
                if (container == "equipment" && item.WH_ID == 100) aliases.Add(CaptureSelectedAlias(client, item));
                rows.Add(new(item, new(container, item.Uid, item.ITEM_ID, item.Position, item.WH_ID, item.Plus,
                    item.StackSize, item.Durability, item.MaximDurability, checked((byte)item.SocketOne), checked((byte)item.SocketTwo), item.Bound, item.Locked)));
            }
        }
        if (inv is not null) Copy(inv, "inventory"); if (equip is not null) Copy(equip, "equipment");
        if (rows.Count != (inv?.Count ?? 0) + (long)(equip?.Count ?? 0)) throw Invalid();
        return new(inventory, equipment, inv, equip, new(player.UID, player.HitPoints, player.Mana),
            rows.OrderBy(row => row.Value.Uid).ToArray(), aliases.OrderBy(alias => alias.Uid).ToArray(), unavailable);
    }

    internal static bool SameNative(Native first, Native second) =>
        ReferenceEquals(first.Inventory, second.Inventory) && ReferenceEquals(first.Equipment, second.Equipment) &&
        ReferenceEquals(first.InventoryMap, second.InventoryMap) && ReferenceEquals(first.EquipmentMap, second.EquipmentMap) &&
        first.Vitals == second.Vitals && first.UnavailableReason == second.UnavailableReason &&
        SameSelectedAliases(first.Aliases, second.Aliases) &&
        first.Rows.Length == second.Rows.Length && first.Rows.Zip(second.Rows).All(pair =>
            ReferenceEquals(pair.First.Source, pair.Second.Source) && pair.First.Value == pair.Second.Value);

    internal static LegacyOwnedPlayerCharacter Character(WorldSession session)
    {
        var c = session.Character ?? throw Invalid();
        return new(c.Uid, c.MapId, c.X, c.Y, c.Body, c.Class, c.Level, c.Money, c.ConquerPoints, c.MinHitPoints, c.MinMana, c.MaxMana);
    }
    private static InvalidDataException Invalid() => new("runtimeInspectionSelectedGraphMalformedOrChanging");
}
