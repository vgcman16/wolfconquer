using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Metadata currentness only. The genuine authenticated creator grants permission.
internal sealed partial class LegacyAuthenticatedCharacterCreationCatalog
{
    private readonly WorldDataCatalog catalog;
    private readonly PlainList<ItemType> rawItems;
    private readonly PlainIndex<IReadOnlyList<ItemType>> itemIndex;
    private readonly ItemRow[] items;
    private readonly PlainList<ProfessionLevelBenefit> rawProfessions;
    private readonly PlainIndex<ProfessionLevelBenefit> professionIndex;
    private readonly ProfessionRow[] professions;
    private readonly PlainList<RuneStorageAttribute> rawRunes;
    private readonly RuneRow[] runes;
    private readonly PlainList<InnerPowerRankEntry> rawInner;
    private readonly PlainIndex<IReadOnlyList<InnerPowerRankEntry>> innerIndex;
    private readonly InnerRow[] inner;
    private readonly KeyValuePair<int, PlainList<InnerPowerRankEntry>>[] innerGroups;
    private readonly LegacyAuthenticatedCharacterCreationMetadata metadata;
    internal LegacyAuthenticatedCharacterCreationMetadata Metadata => metadata.Copy();

    internal LegacyAuthenticatedCharacterCreationCatalog(WorldDataCatalog catalog, uint account, long classCode, uint[] requiredItems)
    {
        ArgumentNullException.ThrowIfNull(catalog); this.catalog = catalog;
        rawItems = new(catalog.Items); itemIndex = new(catalog.ItemsById);
        var dependencies = new List<LegacyAuthenticatedCharacterCreationItemMetadata>(); var captured = new List<ItemRow>();
        foreach (uint id in requiredItems.Distinct().Order())
        {
            var raw = rawItems.Values.Where(row => row.Id == id).ToArray();
            if (raw.Length != 1 || !itemIndex.TryGet(checked((int)id), out var group) || group is null) throw Invalid();
            var values = new PlainList<ItemType>(group); var row = raw[0]; var fields = new PlainList<string>(row.Fields);
            if (values.Values.Length != 1 || !ReferenceEquals(values.Values[0], row) || fields.Values.Length <= ItemType.DurabilityFieldIndex ||
                !uint.TryParse(fields.Values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawId) || rawId != id ||
                fields.Values[1] != row.Name) throw Invalid();
            dependencies.Add(new(id, fields.Values[18], fields.Values[ItemType.DurabilityFieldIndex]));
            captured.Add(new(row, values, fields));
        }
        items = captured.ToArray();
        rawProfessions = new(catalog.ProfessionLevelBenefits); professionIndex = new(catalog.ProfessionLevelBenefitsByProfessionCode);
        professions = rawProfessions.Values.Select(CaptureProfession).ToArray();
        var applicable = professions.Where(row => row.Row.ProfessionCode == classCode).ToArray();
        string[]? profession = null;
        if (applicable.Length > 1) throw Invalid();
        if (applicable.Length == 0)
        { if (professionIndex.TryGet(checked((int)classCode), out _)) throw Invalid(); }
        else
        {
            var row = applicable[0]; var parsed = LegacyAuthenticatedCharacterCreationMetadata.ParseProfession(row.Fields.Values);
            if (!professionIndex.TryGet(checked((int)classCode), out var indexed) || !ReferenceEquals(indexed, row.Row) ||
                parsed.Id != row.Row.Id || parsed.Rank != row.Row.Rank || !parsed.BonusValues.SequenceEqual(row.Bonuses.Values)) throw Invalid();
            profession = row.Fields.Values;
        }
        rawRunes = new(catalog.RuneStorageAttributes); runes = rawRunes.Values.Select(CaptureRune).ToArray();
        rawInner = new(catalog.InnerPowerRanks); innerIndex = new(catalog.InnerPowerRanksByCharacterId);
        inner = rawInner.Values.Select(row => CaptureInner(row, account)).ToArray();
        if (innerIndex.TryGet(checked((int)account), out _)) throw Invalid();
        innerGroups = catalog.InnerPowerRanksByCharacterId.Select(pair =>
            new KeyValuePair<int, PlainList<InnerPowerRankEntry>>(pair.Key, new(pair.Value))).ToArray();
        metadata = new(dependencies.ToArray(), profession, runes.Where(row => row.Row.RequiredStorageScore == 0).Select(row => row.Fields.Values).ToArray(), []);
        if (!IsCurrent()) throw Invalid();
    }

    internal bool IsCurrent()
    {
        if (!rawItems.IsCurrent(catalog.Items) || !itemIndex.IsCurrent(catalog.ItemsById) ||
            !rawProfessions.IsCurrent(catalog.ProfessionLevelBenefits) || !professionIndex.IsCurrent(catalog.ProfessionLevelBenefitsByProfessionCode) ||
            !rawRunes.IsCurrent(catalog.RuneStorageAttributes) || !rawInner.IsCurrent(catalog.InnerPowerRanks) ||
            !innerIndex.IsCurrent(catalog.InnerPowerRanksByCharacterId)) return false;
        foreach (var row in items)
            if (!itemIndex.TryGet(row.Row.Id, out var group) || group is null || !row.IsCurrent(group)) return false;
        if (professions.Any(row => !row.IsCurrent()) || runes.Any(row => !row.IsCurrent()) || inner.Any(row => !row.IsCurrent())) return false;
        foreach (var group in innerGroups)
            if (!innerIndex.TryGet(group.Key, out var current) || current is null || !group.Value.IsCurrent(current)) return false;
        return true;
    }
    private static InvalidDataException Invalid() => new("Fresh creation requires unique current adopted catalog dependencies and no prior InnerPower row.");
}
