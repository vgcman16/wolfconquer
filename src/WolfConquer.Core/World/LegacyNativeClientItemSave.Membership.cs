using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClientItemSave
{
    private enum RootKind { Inventory, Equipment, Warehouse, Wardrobe, Rune, Relic, Gear }
    private sealed record MapImage(object Map, KeyValuePair<uint, LegacyNativeGameItemRuntime>[] Entries)
    {
        internal static MapImage Capture(object map) => new(map, Read(map));
        internal bool IsCurrent() => SameEntries(Entries, Read(Map));
        private static KeyValuePair<uint, LegacyNativeGameItemRuntime>[] Read(object map) => map switch
        {
            ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> concurrent => concurrent.ToArray(),
            LegacyNativeItemBagRuntime bag => bag.ToArray(),
            _ => throw InvalidGraph("invalidContainerMap")
        };
    }

    private sealed class OwnedRoot(SavedItem state, RootKind kind)
    {
        internal SavedItem State { get; } = state;
        internal RootKind Kind { get; } = kind;
        internal bool WardrobeAlias;
        internal int WardrobeBucket = -1;
        internal SavedItem[] Children = [];
        internal bool MainPerfection => Kind is RootKind.Inventory or RootKind.Equipment or RootKind.Warehouse;
    }

    private sealed partial class SaveGraph
    {
        private readonly List<(MapImage Image, RootKind Kind, int Bucket)> maps = [];
        private readonly List<OwnedRoot> roots = [];
        private readonly Dictionary<uint, SavedItem> byUid = [];
        private readonly HashSet<LegacyNativeGameItemRuntime> physical = new(ReferenceEqualityComparer.Instance);

        private void CaptureRoots()
        {
            AddMap(inventoryMap, RootKind.Inventory);
            AddMap(equipmentMap, RootKind.Equipment);
            foreach (var bank in bankRows) AddMap(bank.Value, RootKind.Warehouse);
            for (int index = 0; index < wardrobeMapRows.Length; index++)
                AddMap(wardrobeMapRows[index], RootKind.Wardrobe, index);
            foreach (var row in runeRows) AdmitRoot(row, RootKind.Rune);
            // Native bag keys are indexes; the saved row identity is its genuine UID.
            AddMap(relic, RootKind.Relic);
            AddMap(gear, RootKind.Gear);
        }

        private void AddMap(object? map, RootKind kind, int wardrobeBucket = -1)
        {
            if (map is null) throw InvalidGraph("nullContainerMap");
            var captured = MapImage.Capture(map); maps.Add((captured, kind, wardrobeBucket));
            foreach (var entry in captured.Entries)
            {
                if (entry.Value is null || (kind is not (RootKind.Relic or RootKind.Gear) && entry.Key != entry.Value.Uid))
                    throw InvalidGraph("invalidOwnedEntry");
                if (kind == RootKind.Wardrobe && (entry.Value.WH_ID != 100 ||
                    !IsWardrobeBucket(entry.Value, wardrobeBucket))) throw InvalidGraph("invalidWardrobeEntry");
                AdmitRoot(entry.Value, kind, wardrobeBucket);
            }
        }

        private void AdmitRoot(LegacyNativeGameItemRuntime item, RootKind kind, int wardrobeBucket = -1)
        {
            if (item is null || item.Uid == 0) throw InvalidGraph("invalidRoot");
            if (byUid.TryGetValue(item.Uid, out var prior))
            {
                var root = roots.FirstOrDefault(candidate => ReferenceEquals(candidate.State, prior));
                if (!ReferenceEquals(prior.Item, item) || root is null ||
                    !IsWardrobeEquipmentAlias(root, item, kind, wardrobeBucket)) throw InvalidGraph("duplicateOwnership");
                root.WardrobeAlias = true;
                root.WardrobeBucket = wardrobeBucket;
                return;
            }
            if (!physical.Add(item)) throw InvalidGraph("rekeyedPhysicalRoot");
            var saved = new SavedItem(item);
            byUid.Add(saved.Uid, saved);
            roots.Add(new(saved, kind));
        }

        private static bool IsWardrobeEquipmentAlias(OwnedRoot prior, LegacyNativeGameItemRuntime item,
            RootKind kind, int bucket)
        {
            if (prior.Kind != RootKind.Equipment || kind != RootKind.Wardrobe || prior.WardrobeAlias ||
                item.WH_ID != 100 || item.Position is 0 or > 59 || item.Uid != prior.State.Uid) return false;
            return IsWardrobeBucket(item, bucket);
        }

        private static bool IsWardrobeBucket(LegacyNativeGameItemRuntime item, int bucket)
        {
            ushort nativePosition = LegacyNativeItemPosition.ItemPosition(item.ITEM_ID);
            return (nativePosition == 9 && bucket == 0) || (nativePosition == 17 && bucket == 1);
        }

        private void CaptureDeposits()
        {
            foreach (var root in roots)
            {
                var children = new List<SavedItem>();
                foreach (var entry in root.State.Deposits.Entries)
                {
                    var item = entry.Value;
                    if (item is null || item.Uid == 0 || entry.Key != item.Uid ||
                        byUid.ContainsKey(item.Uid) || !physical.Add(item)) throw InvalidGraph("duplicateOrInvalidDeposit");
                    var child = new SavedItem(item);
                    if (child.Deposits.Entries.Length != 0) throw InvalidGraph("unsupportedNestedDeposit");
                    byUid.Add(child.Uid, child); children.Add(child);
                }
                root.Children = children.ToArray();
            }
        }

        private bool HasCurrentMembership()
        {
            if (!ReferenceEquals(inventory.ClientItems, inventoryMap) || !ReferenceEquals(equipment.ClientItems, equipmentMap) ||
                !ReferenceEquals(warehouse.ClientItems, banks) || !SameBanks(bankRows, banks.ToArray()) ||
                !ReferenceEquals(wardrobe.Items, wardrobeMaps) || wardrobeMaps.Length != wardrobeMapRows.Length)
                return false;
            for (int index = 0; index < wardrobeMapRows.Length; index++)
                if (!ReferenceEquals(wardrobeMaps[index], wardrobeMapRows[index])) return false;
            foreach (var map in maps)
            {
                if (!map.Image.IsCurrent()) return false;
                if (map.Kind == RootKind.Wardrobe && map.Image.Entries.Any(entry =>
                    entry.Value.WH_ID != 100 || !IsWardrobeBucket(entry.Value, map.Bucket))) return false;
            }
            foreach (var root in roots)
                if (root.WardrobeAlias && (root.State.Item.WH_ID != 100 || root.State.Item.Position is 0 or > 59 ||
                    !IsWardrobeBucket(root.State.Item, root.WardrobeBucket))) return false;
            var currentRunes = rune.Objects;
            return currentRunes.Length == runeRows.Length &&
                currentRunes.SequenceEqual(runeRows, ReferenceEqualityComparer.Instance);
        }
    }

    private static bool SameEntries(KeyValuePair<uint, LegacyNativeGameItemRuntime>[] before,
        KeyValuePair<uint, LegacyNativeGameItemRuntime>[] after) => before.Length == after.Length &&
        before.Zip(after).All(pair => pair.First.Key == pair.Second.Key && ReferenceEquals(pair.First.Value, pair.Second.Value));

    private static bool SameBanks(KeyValuePair<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>>[] before,
        KeyValuePair<uint, ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>>[] after) => before.Length == after.Length &&
        before.Zip(after).All(pair => pair.First.Key == pair.Second.Key && ReferenceEquals(pair.First.Value, pair.Second.Value));
}
