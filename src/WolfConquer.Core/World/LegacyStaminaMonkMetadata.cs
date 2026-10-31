using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Exact required12560 raw/index/native-row join; never a generic spell-grant owner.
internal sealed class LegacyStaminaMonkMetadata
{
    private readonly WorldDataCatalog catalog;
    private readonly MagicType[] rawRows, groupRows;
    private readonly IReadOnlyList<MagicType> group;
    private readonly Dictionary<int, IReadOnlyList<MagicType>> index;
    private readonly KeyValuePair<int, IReadOnlyList<MagicType>>[] indexRows;
    private readonly MagicType row;
    private readonly IReadOnlyList<string> fieldSource;
    private readonly string[] fields;
    private readonly LegacyNativeMagicRow native;
    private readonly float damage;
    internal byte Bonus { get; }

    internal LegacyStaminaMonkMetadata(WorldDataCatalog catalog, ushort level)
    {
        if (catalog is null) throw new InvalidDataException("staminaMonkCatalogMissing");
        this.catalog = catalog;
        rawRows = Copy(catalog.Magic);
        if (catalog.MagicByTypeId is null || catalog.MagicByTypeId.GetType() != typeof(Dictionary<int, IReadOnlyList<MagicType>>))
            throw new InvalidOperationException("staminaMonkCatalogIndexUnproven");
        index = (Dictionary<int, IReadOnlyList<MagicType>>)catalog.MagicByTypeId;
        if (!ReferenceEquals(index.Comparer, EqualityComparer<int>.Default))
            throw new InvalidOperationException("staminaMonkCatalogComparerUnproven");
        indexRows = index.ToArray();
        if (!index.TryGetValue(12_560, out var adoptedGroup)) throw new InvalidDataException("staminaMonkMetadataMissing");
        group = adoptedGroup;
        var indexed = indexRows.Where(value => value.Key == 12_560).ToArray();
        if (indexed.Length != 1 || !ReferenceEquals(indexed[0].Value, group))
            throw new InvalidDataException("staminaMonkCatalogIndexMismatch");
        groupRows = Copy(group);
        var required = rawRows.Where(value => value.TypeId == 12_560).ToArray();
        if (required.Length != groupRows.Length || required.Where((value, i) => !ReferenceEquals(value, groupRows[i])).Any())
            throw new InvalidDataException("staminaMonkCatalogIndexMismatch");
        var matching = required.Where(value => ReadLevel(value, catalog.NativeMagicParseCulture) == level).ToArray();
        if (matching.Length != 1) throw new InvalidDataException("staminaMonkMetadataAmbiguousOrMissing");
        row = matching[0]; fieldSource = row.Fields; fields = CopyFields(fieldSource);
        native = catalog.GetNativeMagicRow(row);
        var parsed = LegacyNativeMagicRow.ParseMutableFields(row, catalog.NativeMagicParseCulture);
        damage = native.Damage;
        if (native.Id != 12_560 || native.Level != level || parsed.Id != native.Id || parsed.Level != native.Level ||
            parsed.Damage != damage || !float.IsFinite(damage) || damage < 0 || damage >= 256)
            throw new InvalidDataException("staminaMonkDamageInvalid");
        Bonus = checked((byte)damage); // Source conversion, after finite/range validation.
        if (!IsCurrentPlain()) throw new InvalidDataException("staminaMonkMetadataChanged");
    }

    internal bool IsCurrentPlain()
    {
        if (!Same(catalog.Magic, rawRows) || !Same(group, groupRows) || !ReferenceEquals(catalog.MagicByTypeId, index) ||
            !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default) ||
            index.Count != indexRows.Length || native.Damage != damage || !ReferenceEquals(row.Fields, fieldSource) ||
            !SameFields(fieldSource, fields)) return false;
        var iterator = index.GetEnumerator();
        foreach (var before in indexRows)
            if (!iterator.MoveNext() || iterator.Current.Key != before.Key || !ReferenceEquals(iterator.Current.Value, before.Value)) return false;
        return !iterator.MoveNext();
    }
    private static byte ReadLevel(MagicType row, CultureInfo culture)
    {
        var fields = CopyFields(row.Fields);
        if (fields.Length <= 33 || int.Parse(fields[0], culture) != row.Id || ushort.Parse(fields[1], culture) != 12_560)
            throw new InvalidDataException("staminaMonkRawIdentityInvalid");
        return byte.Parse(fields[8], culture);
    }
    private static MagicType[] Copy(IReadOnlyList<MagicType> source)
    {
        if (source is null) throw new InvalidDataException("staminaMonkRowsUnproven");
        MagicType[] copy;
        if (source.GetType() == typeof(MagicType[])) copy = ((MagicType[])source).ToArray();
        else if (source.GetType() == typeof(List<MagicType>)) copy = ((List<MagicType>)source).ToArray();
        else throw new InvalidDataException("staminaMonkRowsUnproven");
        foreach (var value in copy) if (value is null) throw new InvalidDataException("staminaMonkRowsUnproven");
        return copy;
    }
    private static bool Same(IReadOnlyList<MagicType> source, MagicType[] before)
    {
        if (source is null) return false;
        if (source.GetType() == typeof(MagicType[]))
        {
            var array = (MagicType[])source;
            if (array.Length != before.Length) return false;
            for (int i = 0; i < before.Length; i++) if (!ReferenceEquals(array[i], before[i])) return false;
            return true;
        }
        if (source.GetType() != typeof(List<MagicType>)) return false;
        var list = (List<MagicType>)source;
        if (list.Count != before.Length) return false;
        for (int i = 0; i < before.Length; i++) if (!ReferenceEquals(list[i], before[i])) return false;
        return true;
    }
    private static string[] CopyFields(IReadOnlyList<string> source)
    {
        if (source is null) throw new InvalidDataException("staminaMonkFieldsUnproven");
        return source.GetType() == typeof(string[]) ? ((string[])source).ToArray()
            : source.GetType() == typeof(List<string>) ? ((List<string>)source).ToArray()
            : throw new InvalidDataException("staminaMonkFieldsUnproven");
    }
    private static bool SameFields(IReadOnlyList<string> source, string[] before)
    {
        if (source is null) return false;
        if (source.GetType() == typeof(string[])) return ((string[])source).AsSpan().SequenceEqual(before);
        if (source.GetType() != typeof(List<string>)) return false;
        var list = (List<string>)source;
        if (list.Count != before.Length) return false;
        for (int i = 0; i < before.Length; i++) if (list[i] != before[i]) return false;
        return true;
    }
}
