using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneOperationEvidence
{
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
            for (var i = 0; i < count; i++) Values[i] = array is not null ? array[i] : list![i];
        }
        internal bool IsCurrent()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (var i = 0; i < Values.Length; i++)
                if (!ReferenceEquals(Values[i], array is not null ? array[i] : list![i])) return false;
            return true;
        }
    }

    internal sealed class CatalogWitness
    {
        private readonly WorldDataCatalog catalog;
        private readonly PlainRows<ItemType> raw;
        private readonly Dictionary<int, IReadOnlyList<ItemType>> index;
        private readonly KeyValuePair<int, IReadOnlyList<ItemType>>[] indexed;
        private readonly RowWitness[] rows;
        internal LegacyRuneMetadata[] Rows { get; }
        internal CatalogWitness(WorldDataCatalog catalog, uint[] required)
        {
            this.catalog = catalog; raw = new(catalog.Items);
            if (raw.Values.Any(value => value is null)) throw Invalid();
            index = catalog.ItemsById as Dictionary<int, IReadOnlyList<ItemType>> ?? throw Invalid();
            if (index.GetType() != typeof(Dictionary<int, IReadOnlyList<ItemType>>) ||
                !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default)) throw Invalid();
            indexed = index.ToArray();
            var captured = new List<RowWitness>();
            foreach (var id in required)
            {
                if (id > int.MaxValue || !index.TryGetValue((int)id, out var group)) throw Invalid();
                var row = new RowWitness(catalog, id, group);
                var matching = raw.Values.Where(value => value.Id == id).ToArray();
                if (matching.Length != 1 || !ReferenceEquals(matching[0], row.Item)) throw Invalid();
                captured.Add(row);
            }
            rows = captured.ToArray(); Rows = rows.Select(row => row.Metadata).ToArray();
            if (!IsCurrent()) throw Invalid();
        }
        internal bool IsCurrent()
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
    }

    private sealed class RowWitness
    {
        internal readonly ItemType Item;
        internal readonly LegacyRuneMetadata Metadata;
        private readonly PlainRows<ItemType> group;
        private readonly PlainRows<string> fields;
        private readonly LegacyRuneSpellClassification? classification;
        internal RowWitness(WorldDataCatalog catalog, uint id, IReadOnlyList<ItemType> source)
        {
            group = new(source);
            if (group.Values.Length != 1 || group.Values[0] is not { } item || item.Id != id) throw Invalid();
            Item = item; fields = new(item.Fields);
            if (fields.Values.Length <= ItemType.RuneSpellIdFieldIndex || !uint.TryParse(fields.Values[0],
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) || rawId != id) throw Invalid();
            uint Read(int offset) => uint.TryParse(fields.Values[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : throw Invalid();
            var encoded = Read(ItemType.RuneSpellIdFieldIndex);
            if (!LegacyRuneSpellIdentity.IsNoSkill(id, encoded, LegacyRuneMappingSemantics.RawSelf))
            {
                var spellId = encoded / 100;
                if (spellId is 0 or > ushort.MaxValue || !LegacyRuneSpellClassification.TryCapture(catalog, checked((ushort)spellId), out classification) ||
                    classification is null) throw Invalid();
            }
            Metadata = new(id, encoded, Read(ItemType.RequiredClassFieldIndex), Read(ItemType.RequiredLevelFieldIndex), classification?.IsRuneSkill);
        }
        internal bool IsCurrent() => group.IsCurrent() && ReferenceEquals(Item.Fields, fields.Source) && fields.IsCurrent() &&
            (classification is null || classification.IsCurrent());
    }
}
