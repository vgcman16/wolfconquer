using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Adopted family DATA, not a live monster, spawn, AI event or attack owner.
internal sealed partial class LegacyMonsterPhysicalRangeMetadata
{
    private readonly WorldDataCatalog catalog;
    private readonly Monster family;
    private readonly IReadOnlyDictionary<string, string> capturedAttributes;
    private readonly LegacyNativeAttributeMapProof attributes;
    private readonly FamilyRows rows;
    private readonly Dictionary<int, Monster> index;
    private readonly KeyValuePair<int, Monster>[] indexed;
    internal byte Boss { get; }
    internal int MaximumHitpoints { get; }
    internal sbyte BirthRange { get; }
    internal int FamilyId => family.Id;
    internal bool IsCurrentPlain(WorldDataCatalog actualCatalog) => IsCurrentPlain(actualCatalog, family);

    internal LegacyMonsterPhysicalRangeMetadata(WorldDataCatalog catalog, Monster family)
    {
        this.catalog = catalog ?? throw Invalid(); this.family = family ?? throw Invalid();
        rows = new(catalog.Monsters);
        if (catalog.MonstersById.GetType() != typeof(Dictionary<int, Monster>)) throw Invalid();
        index = (Dictionary<int, Monster>)catalog.MonstersById;
        if (!ReferenceEquals(index.Comparer, EqualityComparer<int>.Default) ||
            !index.TryGetValue(family.Id, out var adopted) || !ReferenceEquals(adopted, family) ||
            rows.Values.Count(row => row.Id == family.Id) != 1 ||
            !rows.Values.Any(row => ReferenceEquals(row, family))) throw Invalid();
        indexed = index.ToArray();
        capturedAttributes = family.Attributes;
        attributes = new(capturedAttributes);
        if (!attributes.Fields.TryGetValue("id", out var id) ||
            !int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) ||
            rawId != family.Id || family.Id <= 0 ||
            !attributes.Fields.TryGetValue("life", out var life) ||
            !int.TryParse(life, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maximum) || maximum <= 0)
            throw Invalid();
        MaximumHitpoints = maximum;
        Boss = ReadBoss(attributes.Fields);
        sbyte loaded = ReadLoadedRange(attributes.Fields);
        if (Boss != 0) loaded = 3; // Actual Server loader override, before Copy.
        BirthRange = Boss > 0 && maximum is > 100_000 and < 7_000_000 ? (sbyte)12 : loaded;
        if (!IsCurrentPlain(catalog, family)) throw Invalid();
    }

    internal bool IsCurrentPlain(WorldDataCatalog actualCatalog, Monster actualFamily)
    {
        if (!ReferenceEquals(catalog, actualCatalog) || !ReferenceEquals(family, actualFamily) ||
            !ReferenceEquals(catalog.Monsters, rows.Source) || !rows.IsCurrentPlain() ||
            !ReferenceEquals(catalog.MonstersById, index) ||
            !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default) || index.Count != indexed.Length ||
            !ReferenceEquals(family.Attributes, capturedAttributes) || !attributes.IsCurrentPlain()) return false;
        var iterator = index.GetEnumerator();
        for (int i = 0; i < indexed.Length; i++)
            if (!iterator.MoveNext() || iterator.Current.Key != indexed[i].Key ||
                !ReferenceEquals(iterator.Current.Value, indexed[i].Value)) return false;
        return !iterator.MoveNext();
    }

    private static byte ReadBoss(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("Boss", out var text)) return 0; // Server510's declared loader default.
        return byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw Invalid();
    }

    private static sbyte ReadLoadedRange(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("attack_range", out var text)) return 0; // Server494's declared loader default.
        return sbyte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : throw Invalid();
    }

    private static InvalidDataException Invalid() => new("physicalMonsterRangeFamilyMissingMalformedOrUnproven");
}
