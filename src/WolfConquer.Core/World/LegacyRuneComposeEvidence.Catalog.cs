using System.Globalization;
using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneComposeEvidence
{
    // Materials include real essence family404. Prove their actual adopted raw
    // and indexed rows without inventing a Rune mapping/classification for them.
    private sealed class MaterialCatalogWitness
    {
        private readonly WorldDataCatalog catalog;
        private readonly PlainRows<ItemType> raw;
        private readonly Dictionary<int, IReadOnlyList<ItemType>> index;
        private readonly KeyValuePair<int, IReadOnlyList<ItemType>>[] indexed;
        private readonly MaterialRow[] rows;
        internal IReadOnlyDictionary<uint, LegacyRuneComposeMaterialMetadata> Lookup { get; }

        internal MaterialCatalogWitness(WorldDataCatalog catalog, uint[] required)
        {
            this.catalog = catalog; raw = new(catalog.Items);
            if (raw.Values.Any(row => row is null)) throw Invalid();
            index = catalog.ItemsById as Dictionary<int, IReadOnlyList<ItemType>> ?? throw Invalid();
            if (index.GetType() != typeof(Dictionary<int, IReadOnlyList<ItemType>>) ||
                !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default)) throw Invalid();
            indexed = index.ToArray();
            rows = required.Select(id =>
            {
                if (id is 0 or > int.MaxValue || !index.TryGetValue((int)id, out var group)) throw Invalid();
                var row = new MaterialRow(id, group);
                var matches = raw.Values.Where(item => item.Id == id).ToArray();
                if (matches.Length != 1 || !ReferenceEquals(matches[0], row.Item)) throw Invalid();
                return row;
            }).ToArray();
            // A malformed cap is not zero/nonstack. Omit it so a reached
            // material refuses, while source-ignored max-level tails stay inert.
            var caps = new Dictionary<uint, LegacyRuneComposeMaterialMetadata>();
            foreach (var row in rows)
                if (row.MaximumStackSize is { } cap)
                    caps.Add(checked((uint)row.Item.Id), new(checked((uint)row.Item.Id), cap));
            Lookup = new ReadOnlyDictionary<uint, LegacyRuneComposeMaterialMetadata>(caps);
            if (!IsCurrent()) throw Invalid();
        }

        internal bool IsCurrent()
        {
            try
            {
                if (!ReferenceEquals(catalog.Items, raw.Source) || !ReferenceEquals(catalog.ItemsById, index) ||
                    !raw.IsCurrent() || index.Count != indexed.Length) return false;
                int position = 0;
                foreach (var entry in index)
                {
                    var old = indexed[position++];
                    if (entry.Key != old.Key || !ReferenceEquals(entry.Value, old.Value)) return false;
                }
                return rows.All(row => row.IsCurrent());
            }
            catch (InvalidOperationException) { return false; }
        }
    }

    private sealed class MaterialRow
    {
        internal readonly ItemType Item;
        internal readonly ushort? MaximumStackSize;
        private readonly PlainRows<ItemType> group;
        private readonly PlainRows<string> fields;
        internal MaterialRow(uint id, IReadOnlyList<ItemType> source)
        {
            group = new(source);
            if (group.Values.Length != 1 || group.Values[0] is not { } item || item.Id != id) throw Invalid();
            Item = item; fields = new(item.Fields);
            if (fields.Values.Length == 0 || fields.Values.Any(field => field is null) ||
                !uint.TryParse(fields.Values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) || rawId != id)
                throw Invalid();
            MaximumStackSize = fields.Values.Length > ItemType.StackSizeFieldIndex &&
                ushort.TryParse(fields.Values[ItemType.StackSizeFieldIndex], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var cap) ? cap : null;
        }
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
            int count = array?.Length ?? list!.Count;
            Values = new T[count];
            for (var index = 0; index < count; index++) Values[index] = array is not null ? array[index] : list![index];
        }
        internal bool IsCurrent()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (var index = 0; index < Values.Length; index++)
                if (!ReferenceEquals(Values[index], array is not null ? array[index] : list![index])) return false;
            return true;
        }
    }
}
