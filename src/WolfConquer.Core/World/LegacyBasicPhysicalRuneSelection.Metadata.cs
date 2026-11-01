using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyBasicPhysicalRuneSelection
{
    // No skill mapping/classification or required-level gate belongs here.
    private sealed class Metadata
    {
        private readonly WorldDataCatalog catalog;
        private readonly BclRows<ItemType> raw;
        private readonly Dictionary<int, IReadOnlyList<ItemType>> index;
        private readonly KeyValuePair<int, IReadOnlyList<ItemType>>[] indexed;
        private readonly RequiredRow[] rows;
        internal Metadata(WorldDataCatalog? catalog, uint[] required)
        {
            this.catalog = catalog ?? throw Invalid(); raw = new(catalog.Items);
            if (catalog.ItemsById is null || catalog.ItemsById.GetType() != typeof(Dictionary<int, IReadOnlyList<ItemType>>)) throw Invalid();
            index = (Dictionary<int, IReadOnlyList<ItemType>>)catalog.ItemsById;
            if (!ReferenceEquals(index.Comparer, EqualityComparer<int>.Default)) throw Invalid();
            indexed = index.ToArray();
            rows = required.Select(id =>
            {
                if (id is 0 or > int.MaxValue || !index.TryGetValue((int)id, out var group)) throw Invalid();
                var row = new RequiredRow(id, group);
                var matches = raw.Values.Where(item => item.Id == id).ToArray();
                if (matches.Length != 1 || !ReferenceEquals(matches[0], row.Item)) throw Invalid();
                return row;
            }).ToArray();
            if (!IsCurrentPlain(catalog)) throw Invalid();
        }
        internal (string Name, uint Class) Require(uint id)
        {
            var row = rows.Single(value => value.Item.Id == id);
            return (row.Name, row.Class);
        }
        internal bool IsCurrentPlain(WorldDataCatalog? actual)
        {
            if (!ReferenceEquals(catalog, actual) || !ReferenceEquals(catalog.Items, raw.Source) || !raw.IsCurrentPlain() ||
                !ReferenceEquals(catalog.ItemsById, index) || !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default) ||
                index.Count != indexed.Length) return false;
            var iterator = index.GetEnumerator();
            for (int i = 0; i < indexed.Length; i++)
                if (!iterator.MoveNext() || iterator.Current.Key != indexed[i].Key ||
                    !ReferenceEquals(iterator.Current.Value, indexed[i].Value)) return false;
            if (iterator.MoveNext()) return false;
            for (int i = 0; i < rows.Length; i++) if (!rows[i].IsCurrentPlain()) return false;
            return true;
        }
    }

    private sealed class RequiredRow
    {
        internal readonly ItemType Item;
        internal readonly string Name;
        internal readonly uint Class;
        private readonly BclRows<ItemType> group;
        private readonly BclRows<string> fields;
        internal RequiredRow(uint id, IReadOnlyList<ItemType> source)
        {
            group = new(source);
            if (group.Values.Length != 1 || group.Values[0].Id != id) throw Invalid();
            Item = group.Values[0]; fields = new(Item.Fields);
            if (fields.Values.Length <= 2 ||
                !uint.TryParse(fields.Values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) || rawId != id ||
                string.IsNullOrWhiteSpace(fields.Values[1]) || !string.Equals(Item.Name, fields.Values[1], StringComparison.Ordinal) ||
                !uint.TryParse(fields.Values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var job)) throw Invalid();
            Name = fields.Values[1]; Class = job;
        }
        internal bool IsCurrentPlain() => ReferenceEquals(Item.Fields, fields.Source) && group.IsCurrentPlain() && fields.IsCurrentPlain();
    }

    private sealed class BclRows<T> where T : class
    {
        internal readonly IReadOnlyList<T> Source;
        internal readonly T[] Values;
        private readonly T[]? array;
        private readonly List<T>? list;
        internal BclRows(IReadOnlyList<T> source)
        {
            Source = source ?? throw Invalid();
            if (source.GetType() == typeof(T[])) array = (T[])source;
            else if (source.GetType() == typeof(List<T>)) list = (List<T>)source;
            else throw Invalid();
            int count = array?.Length ?? list!.Count; Values = new T[count];
            for (int i = 0; i < count; i++) Values[i] = (array is not null ? array[i] : list![i]) ?? throw Invalid();
        }
        internal bool IsCurrentPlain()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (int i = 0; i < Values.Length; i++)
                if (!ReferenceEquals(Values[i], array is not null ? array[i] : list![i])) return false;
            return true;
        }
    }

    private static InvalidDataException Invalid() => new("basicPhysicalRuneSelectionMetadataUnproven");
}
