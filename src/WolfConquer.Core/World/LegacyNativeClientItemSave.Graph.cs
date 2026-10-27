using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClientItemSave
{
    private sealed partial class SaveGraph
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly SaveLocation location;
        private readonly LegacyNativeInventoryRuntime inventory;
        private readonly LegacyNativeEquipItemStorage equipment;
        private readonly LegacyNativeWarehouseRuntime warehouse;
        private readonly LegacyNativeWardrobeRuntime wardrobe;
        private readonly LegacyNativeRuneItemStorage rune;
        private readonly LegacyNativeItemBagRuntime relic;
        private readonly LegacyNativeItemBagRuntime gear;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> inventoryMap;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> equipmentMap;
        private readonly ConcurrentDictionary<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>> banks;
        private readonly KeyValuePair<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>>[] bankRows;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] wardrobeMaps;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[] wardrobeMapRows;
        private readonly LegacyNativeGameItemRuntime[] runeRows;

        private SaveGraph(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player, SaveLocation location)
        {
            this.client = client; this.player = player; this.location = location;
            inventory = client.Inventory ?? throw InvalidGraph("missingInventory");
            equipment = client.Equipment ?? throw InvalidGraph("missingEquipment");
            warehouse = client.Warehouse ?? throw InvalidGraph("missingWarehouse");
            wardrobe = client.MyWardrobe ?? throw InvalidGraph("missingWardrobe");
            rune = client.Rune ?? throw InvalidGraph("missingRune");
            relic = client.RelicFuse ?? throw InvalidGraph("missingRelicBag");
            gear = client.GearMythsoulBag ?? throw InvalidGraph("missingGearBag");
            inventoryMap = inventory.ClientItems ?? throw InvalidGraph("missingInventoryMap");
            equipmentMap = equipment.ClientItems ?? throw InvalidGraph("missingEquipmentMap");
            banks = warehouse.ClientItems ?? throw InvalidGraph("missingBanks");
            bankRows = banks.ToArray();
            wardrobeMaps = wardrobe.Items ?? throw InvalidGraph("missingWardrobeMaps");
            if (wardrobeMaps.Length != 2 || wardrobeMaps.Any(map => map is null) ||
                ReferenceEquals(wardrobeMaps[0], wardrobeMaps[1])) throw InvalidGraph("invalidWardrobeMaps");
            wardrobeMapRows = wardrobeMaps.ToArray();
            runeRows = rune.Objects;
            if (!HasCurrentOwners()) throw InvalidGraph("invalidPhysicalOwner");
        }

        internal static SaveGraph Capture(LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, SaveLocation location)
        {
            var graph = new SaveGraph(client, player, location);
            graph.CaptureRoots();
            graph.CaptureDeposits();
            if (!graph.HasCurrentOwners() || !graph.HasCurrentMembership()) throw InvalidGraph("changedMembership");
            return graph;
        }

        private bool HasCurrentOwners() => ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && location.IsCurrent(player) &&
            ReferenceEquals(client.Inventory, inventory) && ReferenceEquals(inventory.Owner, client) &&
            ReferenceEquals(client.Equipment, equipment) && equipment.IsOwnedBy(client) &&
            ReferenceEquals(client.Warehouse, warehouse) && ReferenceEquals(warehouse.User, client) &&
            ReferenceEquals(client.MyWardrobe, wardrobe) && wardrobe.IsOwnedBy(client) &&
            ReferenceEquals(client.Rune, rune) && rune.IsSaveOwnedBy(client) &&
            ReferenceEquals(client.RelicFuse, relic) && ReferenceEquals(client.GearMythsoulBag, gear);

        internal bool IsCurrent()
        {
            if (!HasCurrentOwners() || !HasCurrentMembership()) return false;
            foreach (var root in roots)
            {
                if (!root.State.IsCurrent()) return false;
                foreach (var child in root.Children) if (!child.IsCurrent()) return false;
            }
            return HasCurrentOwners();
        }
    }

    private static InvalidDataException InvalidGraph(string reason) =>
        new("Invalid native item save graph: " + reason);
}
