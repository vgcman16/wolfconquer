using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using ItemMap = System.Collections.Concurrent.ConcurrentDictionary<uint, WolfConquer.Core.World.LegacyNativeGameItemRuntime>;
using BanksMap = System.Collections.Concurrent.ConcurrentDictionary<uint, System.Collections.Concurrent.ConcurrentDictionary<uint, WolfConquer.Core.World.LegacyNativeGameItemRuntime>>;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeOwnedItemGraph
{
    private interface IMapWitness
    {
        bool Consume();
    }

    private interface IMapAnchor
    {
        bool GuardedCurrent();
        IMapWitness CreateWitness();
    }

    private sealed class MapAnchor<T> : IMapAnchor where T : class
    {
        private readonly ConcurrentDictionary<uint, T> map;
        private readonly KeyValuePair<uint, T>[] rows;
        private readonly IEnumerator<KeyValuePair<uint, T>> reader;
        internal MapAnchor(ConcurrentDictionary<uint, T> map)
        {
            this.map = map;
            rows = map.ToArray();
            reader = map.GetEnumerator();
        }
        private bool Read(IEnumerator<KeyValuePair<uint, T>> source)
        {
            foreach (var row in rows)
                if (!source.MoveNext() || source.Current.Key != row.Key || !ReferenceEquals(source.Current.Value, row.Value))
                    return false;
            return !source.MoveNext();
        }
        public bool GuardedCurrent()
        {
            reader.Reset();
            return Read(reader);
        }
        public IMapWitness CreateWitness() => new Witness(this, map.GetEnumerator());
        private sealed class Witness(MapAnchor<T> owner, IEnumerator<KeyValuePair<uint, T>> reader) : IMapWitness
        {
            private bool consumed;
            public bool Consume()
            {
                if (consumed)
                    return false;
                consumed = true;
                return owner.Read(reader);
            }
        }
    }

    private sealed class StateWitness(IMapWitness[] maps)
    {
        private bool consumed;
        internal bool Consume()
        {
            if (consumed)
                return false;
            consumed = true;
            foreach (var map in maps)
                if (!map.Consume())
                    return false;
            return true;
        }
    }

    private sealed class PhysicalState
    {
        internal readonly ItemMap Inventory, Equipment;
        internal readonly BanksMap Banks;
        internal readonly ItemMap[] Wardrobe;
        internal readonly LegacyNativeRuneItemStorage.JoinedState Rune;
        internal readonly LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime>.JoinedState Relic, Gear;
        private readonly ItemMap garment, mount;
        private readonly IMapAnchor[] maps;
        internal PhysicalState(ItemMap inventory, ItemMap equipment, BanksMap banks, ItemMap[] wardrobe, LegacyNativeRuneItemStorage.JoinedState rune, LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime>.JoinedState relic, LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime>.JoinedState gear, LegacyOwnedNativeItemImage? image)
        {
            Inventory = inventory;
            Equipment = equipment;
            Banks = banks;
            Wardrobe = wardrobe;
            Rune = rune;
            Relic = relic;
            Gear = gear;
            if (wardrobe.Length != 2 || wardrobe.Any(map => map is null))
                throw Invalid();
            garment = wardrobe[0];
            mount = wardrobe[1];
            var list = new List<IMapAnchor>
            {
                new MapAnchor<LegacyNativeGameItemRuntime>(inventory), new MapAnchor<LegacyNativeGameItemRuntime>(equipment), new MapAnchor<ItemMap>(banks), new MapAnchor<LegacyNativeGameItemRuntime>(garment), new MapAnchor<LegacyNativeGameItemRuntime>(mount)
            };
            foreach (var bank in banks)
                list.Add(new MapAnchor<LegacyNativeGameItemRuntime>(bank.Value));
            if (image is not null)
            {
                var visited = new HashSet<LegacyNativeGameItemRuntime>(ReferenceEqualityComparer.Instance);
                foreach (var node in image.Roots)
                    AddDeposits(node.Item, visited, list);
            }
            maps = list.ToArray();
        }
        private static void AddDeposits(LegacyNativeGameItemRuntime item, HashSet<LegacyNativeGameItemRuntime> seen, List<IMapAnchor> list)
        {
            if (!seen.Add(item))
                throw Invalid();
            list.Add(new MapAnchor<LegacyNativeGameItemRuntime>(item.Deposite));
            foreach (var child in item.Deposite)
                AddDeposits(child.Value, seen, list);
        }
        internal bool ReferencesCurrentPlain(LegacyNativeOwnedItemGraph graph) =>
            ReferenceEquals(graph.Inventory.ClientItems, Inventory) &&
                ReferenceEquals(graph.Equipment.ClientItems, Equipment) &&
                ReferenceEquals(graph.Warehouse.ClientItems, Banks) &&
                ReferenceEquals(graph.Wardrobe.Items, Wardrobe) &&
                Wardrobe.Length == 2 &&
                ReferenceEquals(Wardrobe[0], garment) &&
                ReferenceEquals(Wardrobe[1], mount) &&
                Rune.ReferencesCurrentPlain() &&
                Relic.ReferencesCurrentPlain() &&
                Gear.ReferencesCurrentPlain();
        internal bool ContentsCurrentPlain()
        {
            try
            {
                foreach (var map in maps)
                    if (!map.GuardedCurrent())
                        return false;
                return Rune.ContentsCurrentPlain() && Relic.ContentsCurrentPlain() && Gear.ContentsCurrentPlain();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        internal StateWitness CreateWitness() => new(maps.Select(map => map.CreateWitness()).ToArray());
        internal bool PrivateContentsCurrentPlain() =>
            Rune.PreparedContentsCurrentPlain() && Relic.ContentsCurrentPlain() && Gear.ContentsCurrentPlain();
        internal void PublishUnchecked(LegacyNativeOwnedItemGraph graph)
        {
            graph.Inventory.ClientItems = Inventory;
            graph.Equipment.ClientItems = Equipment;
            graph.Warehouse.ClientItems = Banks;
            graph.Wardrobe.Items = Wardrobe;
            Rune.PublishUnchecked();
            Relic.PublishUnchecked();
            Gear.PublishUnchecked();
        }
    }

}
