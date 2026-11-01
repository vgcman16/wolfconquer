using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// An adopted selected15920 row witness, not permission to cast or grant a flag.
internal sealed class LegacyOwnedHawksAmbitionMetadata
{
    private readonly WorldDataCatalog catalog;
    private readonly Rows<MagicType> raw, group;
    private readonly Rows<string> fields;
    private readonly Dictionary<int, IReadOnlyList<MagicType>> byType;
    private readonly KeyValuePair<int, IReadOnlyList<MagicType>>[] typeEntries;
    private readonly Dictionary<int, MagicType> byId;
    private readonly KeyValuePair<int, MagicType>[] idEntries;
    private readonly MagicType row;
    private readonly LegacyNativeMagicRow native;
    private readonly float damage;
    private readonly ushort cooldown;
    private readonly (MagicType Row, Rows<string> Fields, byte Level)[] levels;
    internal uint Power { get; }
    internal uint Duration { get; }

    internal LegacyOwnedHawksAmbitionMetadata(WorldDataCatalog catalog, ushort level, LegacyMagicRuntimeSpell spell)
    {
        this.catalog = catalog ?? throw Invalid();
        if (spell.Id != 15_920 || spell.Sort != LegacyMagicSort.AttachStatus || spell.Level != level) throw Invalid();
        raw = new(catalog.Magic);
        if (catalog.MagicByTypeId is null || catalog.MagicById is null ||
            catalog.MagicByTypeId.GetType() != typeof(Dictionary<int, IReadOnlyList<MagicType>>) ||
            catalog.MagicById.GetType() != typeof(Dictionary<int, MagicType>)) throw Invalid();
        byType = (Dictionary<int, IReadOnlyList<MagicType>>)catalog.MagicByTypeId;
        byId = (Dictionary<int, MagicType>)catalog.MagicById;
        if (!ReferenceEquals(byType.Comparer, EqualityComparer<int>.Default) ||
            !ReferenceEquals(byId.Comparer, EqualityComparer<int>.Default) || !byType.TryGetValue(15_920, out var source)) throw Invalid();
        typeEntries = byType.ToArray(); idEntries = byId.ToArray(); group = new(source);
        var required = raw.Values.Where(value => value.TypeId == 15_920).ToArray();
        if (required.Length != group.Values.Length || required.Where((value, i) => !ReferenceEquals(value, group.Values[i])).Any()) throw Invalid();
        levels = required.Select(value =>
        {
            var captured = new Rows<string>(value.Fields);
            if (captured.Values.Length <= 8 || !byte.TryParse(captured.Values[8], NumberStyles.Integer,
                    catalog.NativeMagicParseCulture, out var parsedLevel)) throw Invalid();
            return (Row: value, Fields: captured, Level: parsedLevel);
        }).ToArray();
        var matching = levels.Where(value => value.Level == level).Select(value => value.Row).ToArray();
        if (matching.Length != 1) throw Invalid();
        row = matching[0]; fields = new(row.Fields);
        var f = fields.Values; var culture = catalog.NativeMagicParseCulture;
        if (f.Length <= 33 || !int.TryParse(f[0], NumberStyles.Integer, culture, out var rawId) || rawId != row.Id ||
            !ushort.TryParse(f[1], NumberStyles.Integer, culture, out var id) || id != 15_920 ||
            !byte.TryParse(f[2], NumberStyles.Integer, culture, out var sort) || sort != (byte)LegacyMagicSort.AttachStatus ||
            !uint.TryParse(f[13], NumberStyles.Integer, culture, out var duration) || duration > int.MaxValue || duration != spell.Duration ||
            !byId.TryGetValue(row.Id, out var indexed) || !ReferenceEquals(indexed, row)) throw Invalid();
        native = catalog.GetNativeMagicRow(row);
        var parsed = LegacyNativeMagicRow.ParseMutableFields(row, culture);
        damage = native.Damage; cooldown = native.CoolDown;
        if (!ReferenceEquals(native.Source, row) || native.Id != 15_920 || native.Level != level ||
            parsed.Id != native.Id || parsed.Level != native.Level || parsed.Damage != damage ||
            parsed.CoolDown != cooldown || parsed.GDamage != native.GDamage || !float.IsFinite(damage) || damage < 0)
            throw Invalid();
        Power = checked((uint)damage); Duration = duration;
        // The actual native parser owns Damage transformations. A public DTO's
        // raw Damage is not substituted for that selected adopted row.
        if (!IsCurrentPlain(catalog)) throw Invalid();
    }

    internal bool IsCurrentPlain(WorldDataCatalog? actual)
    {
        if (!ReferenceEquals(catalog, actual) || !ReferenceEquals(catalog.Magic, raw.Source) || !raw.IsCurrentPlain() ||
            !ReferenceEquals(catalog.MagicByTypeId, byType) || !ReferenceEquals(catalog.MagicById, byId) ||
            !ReferenceEquals(byType.Comparer, EqualityComparer<int>.Default) || !ReferenceEquals(byId.Comparer, EqualityComparer<int>.Default) ||
            byType.Count != typeEntries.Length || byId.Count != idEntries.Length || !group.IsCurrentPlain() ||
            !ReferenceEquals(row.Fields, fields.Source) || !fields.IsCurrentPlain() || native.Damage != damage || native.CoolDown != cooldown)
            return false;
        var types = byType.GetEnumerator();
        for (int i = 0; i < typeEntries.Length; i++)
            if (!types.MoveNext() || types.Current.Key != typeEntries[i].Key || !ReferenceEquals(types.Current.Value, typeEntries[i].Value)) return false;
        var ids = byId.GetEnumerator();
        for (int i = 0; i < idEntries.Length; i++)
            if (!ids.MoveNext() || ids.Current.Key != idEntries[i].Key || !ReferenceEquals(ids.Current.Value, idEntries[i].Value)) return false;
        for (int i = 0; i < levels.Length; i++)
            if (!ReferenceEquals(levels[i].Row.Fields, levels[i].Fields.Source) || !levels[i].Fields.IsCurrentAt(8)) return false;
        return !types.MoveNext() && !ids.MoveNext();
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
            Values = new T[array?.Length ?? list!.Count];
            for (int i = 0; i < Values.Length; i++) Values[i] = (array is not null ? array[i] : list![i]) ?? throw Invalid();
        }
        internal bool IsCurrentPlain()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (int i = 0; i < Values.Length; i++)
                if (!ReferenceEquals(Values[i], array is not null ? array[i] : list![i])) return false;
            return true;
        }
        internal bool IsCurrentAt(int index) => (array?.Length ?? list!.Count) == Values.Length &&
            ReferenceEquals(Values[index], array is not null ? array[index] : list![index]);
    }

    private static InvalidDataException Invalid() => new("ownedHawksAmbitionMetadataMissingMalformedOrUnproven");
}
