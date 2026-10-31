using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneDecomposeEvidence
{
    private sealed class RewardCatalog
    {
        private readonly WorldDataCatalog catalog;
        private readonly PlainRows<ItemType> raw;
        private readonly Dictionary<int, IReadOnlyList<ItemType>> index;
        private readonly KeyValuePair<int, IReadOnlyList<ItemType>>[] indexed;
        private readonly ItemRow[] rows;
        internal LegacyRuneDecomposeRewardMetadata[] Metadata { get; }
        internal IReadOnlyDictionary<uint, LegacyRuneDecomposeRewardCap> Caps { get; }

        internal RewardCatalog(WorldDataCatalog catalog, uint target, uint essence, uint fragment)
        {
            this.catalog = catalog; raw = new(catalog.Items);
            if (raw.Values.Any(row => row is null)) throw Invalid();
            index = catalog.ItemsById as Dictionary<int, IReadOnlyList<ItemType>> ?? throw Invalid();
            if (index.GetType() != typeof(Dictionary<int, IReadOnlyList<ItemType>>) ||
                !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default)) throw Invalid();
            indexed = index.ToArray();
            rows = new[] { target, essence, fragment }.Distinct().Order().Select(id =>
            {
                if (id is 0 or > int.MaxValue || !index.TryGetValue((int)id, out var group)) throw Invalid();
                var row = new ItemRow(id, group);
                var matching = raw.Values.Where(item => item.Id == id).ToArray();
                if (matching.Length != 1 || !ReferenceEquals(matching[0], row.Item)) throw Invalid();
                return row;
            }).ToArray();
            Metadata = new[] { essence, fragment }.Order().Select(id =>
            {
                var row = rows.Single(row => row.Item.Id == id);
                var cap = row.ReadUShort(ItemType.StackSizeFieldIndex);
                // The original selected rewards have no timed-item creation.
                // Do not invent an uncaptured clock for a changed catalog row.
                uint time = row.ReadUInt(ItemType.TimeFieldIndex);
                if (time != 0) throw Invalid();
                return new LegacyRuneDecomposeRewardMetadata(id, cap,
                    id == fragment ? row.ReadUShort(ItemType.DurabilityFieldIndex) : null, time);
            }).ToArray();
            Caps = Metadata.ToDictionary(row => row.ItemId, row => new LegacyRuneDecomposeRewardCap(row.ItemId, row.MaximumStackSize));
            if (!IsCurrent()) throw Invalid();
        }

        internal bool IsCurrent()
        {
            if (!ReferenceEquals(catalog.Items, raw.Source) || !ReferenceEquals(catalog.ItemsById, index) ||
                !raw.IsCurrent() || index.Count != indexed.Length) return false;
            int position = 0;
            foreach (var row in index)
            {
                var old = indexed[position++];
                if (row.Key != old.Key || !ReferenceEquals(row.Value, old.Value)) return false;
            }
            return rows.All(row => row.IsCurrent());
        }
    }

    private sealed class ItemRow
    {
        internal readonly ItemType Item;
        private readonly PlainRows<ItemType> group;
        private readonly PlainRows<string> fields;
        internal ItemRow(uint id, IReadOnlyList<ItemType> source)
        {
            group = new(source);
            if (group.Values.Length != 1 || group.Values[0] is not { } item || item.Id != id) throw Invalid();
            Item = item; fields = new(item.Fields);
            if (fields.Values.Any(field => field is null) || ReadUInt(0) != id) throw Invalid();
        }
        internal uint ReadUInt(int offset) => fields.Values.Length > offset && uint.TryParse(fields.Values[offset],
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw Invalid();
        internal ushort ReadUShort(int offset) => fields.Values.Length > offset && ushort.TryParse(fields.Values[offset],
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw Invalid();
        internal bool IsCurrent() => group.IsCurrent() && ReferenceEquals(Item.Fields, fields.Source) && fields.IsCurrent();
    }

    private sealed class PlainRows<T> where T : class
    {
        internal readonly IReadOnlyList<T> Source;
        internal readonly T[] Values;
        private readonly T[]? array;
        private readonly List<T>? list;
        internal PlainRows(IReadOnlyList<T> source)
        {
            Source = source; array = source as T[]; list = source as List<T>;
            if (array is null && (list is null || list.GetType() != typeof(List<T>))) throw Invalid();
            Values = new T[array?.Length ?? list!.Count];
            for (int index = 0; index < Values.Length; index++) Values[index] = array is not null ? array[index] : list![index];
        }
        internal bool IsCurrent()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (int index = 0; index < Values.Length; index++)
                if (!ReferenceEquals(Values[index], array is not null ? array[index] : list![index])) return false;
            return true;
        }
    }
}
