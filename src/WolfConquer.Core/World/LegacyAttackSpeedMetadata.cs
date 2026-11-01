using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Required selected-query metadata only. This witness grants no query or attack.
internal sealed partial class LegacyAttackSpeedMetadata
{
    private readonly WorldDataCatalog? catalog;
    private readonly Rows<ItemType>? raw;
    private readonly Dictionary<int, IReadOnlyList<ItemType>>? index;
    private readonly KeyValuePair<int, IReadOnlyList<ItemType>>[] indexed;
    private readonly Row[] used;
    private readonly bool noSelectedRows;
    internal ushort Right { get; }
    internal ushort Left { get; }
    internal ushort Ring { get; }

    internal LegacyAttackSpeedMetadata(WorldDataCatalog? catalog, uint right, uint left, uint ring)
    {
        this.catalog = catalog;
        noSelectedRows = right == 0 && left == 0 && ring == 0;
        if (noSelectedRows)
        {
            // The actual selected slots contain no item. Their source values
            // are zero without consulting unrelated catalog rows or wrappers.
            // Pin the catalog identity; this DATA is not a query/attack owner.
            indexed = []; used = []; return;
        }
        if (catalog is null) throw Invalid();
        raw = new(catalog.Items);
        if (catalog.ItemsById is null || catalog.ItemsById.GetType() != typeof(Dictionary<int, IReadOnlyList<ItemType>>)) throw Invalid();
        index = (Dictionary<int, IReadOnlyList<ItemType>>)catalog.ItemsById;
        if (!ReferenceEquals(index.Comparer, EqualityComparer<int>.Default)) throw Invalid();
        indexed = index.ToArray();
        used = new[] { right, left, ring }.Where(id => id != 0).Distinct().Order().Select(id =>
        {
            if (id > int.MaxValue || !index.TryGetValue((int)id, out var group)) throw Invalid();
            var row = new Row(id, group);
            var matches = raw.Values.Where(item => item.Id == id).ToArray();
            if (matches.Length != 1 || !ReferenceEquals(matches[0], row.Item)) throw Invalid();
            return row;
        }).ToArray();
        Right = Value(right); Left = Value(left); Ring = Value(ring);
        RightRange = ReadAttackRange(right); LeftRange = ReadAttackRange(left);
        if (!IsCurrentPlain(catalog)) throw Invalid();
    }

    private ushort Value(uint id) => id == 0 ? (ushort)0 : used.Single(row => row.Item.Id == id).Frequency;

    internal bool IsCurrentPlain(WorldDataCatalog? actual)
    {
        if (!ReferenceEquals(catalog, actual)) return false;
        if (noSelectedRows) return raw is null && index is null && used.Length == 0 && Right == 0 && Left == 0 && Ring == 0;
        if (catalog is null) return false;
        if (raw is null || index is null || !ReferenceEquals(catalog.Items, raw.Source) || !raw.IsCurrentPlain() ||
            !ReferenceEquals(catalog.ItemsById, index) || !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default) ||
            index.Count != indexed.Length) return false;
        var iterator = index.GetEnumerator();
        for (int i = 0; i < indexed.Length; i++)
            if (!iterator.MoveNext() || iterator.Current.Key != indexed[i].Key ||
                !ReferenceEquals(iterator.Current.Value, indexed[i].Value)) return false;
        if (iterator.MoveNext()) return false;
        for (int i = 0; i < used.Length; i++) if (!used[i].IsCurrentPlain()) return false;
        return true;
    }

    private sealed class Row
    {
        internal readonly ItemType Item;
        internal readonly ushort Frequency;
        private readonly Rows<ItemType> group;
        private readonly Rows<string> fields;
        internal Row(uint id, IReadOnlyList<ItemType> source)
        {
            group = new(source);
            if (group.Values.Length != 1 || group.Values[0].Id != id) throw Invalid();
            Item = group.Values[0]; fields = new(Item.Fields);
            if (fields.Values.Length <= ItemType.FrequencyFieldIndex ||
                !uint.TryParse(fields.Values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) || rawId != id ||
                !ushort.TryParse(fields.Values[ItemType.FrequencyFieldIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frequency))
                throw Invalid();
            Frequency = frequency;
        }
        internal bool IsCurrentPlain() => group.IsCurrentPlain() && ReferenceEquals(Item.Fields, fields.Source) && fields.IsCurrentPlain();
    }

    private sealed class Rows<T> where T : class
    {
        internal readonly IReadOnlyList<T> Source;
        internal readonly T[] Values;
        private readonly T[]? array;
        private readonly List<T>? list;
        internal Rows(IReadOnlyList<T> source)
        {
            Source = source ?? throw Invalid();
            if (source.GetType() == typeof(T[])) array = (T[])source;
            else if (source.GetType() == typeof(List<T>)) list = (List<T>)source;
            else throw Invalid();
            int count = array?.Length ?? list!.Count;
            Values = new T[count];
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

    private static InvalidDataException Invalid() => new("attackSpeedMetadataMissingMalformedOrUnproven");
}
