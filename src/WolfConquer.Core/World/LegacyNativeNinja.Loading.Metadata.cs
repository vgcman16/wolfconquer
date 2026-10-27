namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private readonly record struct SkillMetadata(uint Key, LegacyNativeNinjaFile.NinjaSprintItem Row,
        uint Id, uint Level, uint First, uint Second);

    private static (Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? Table, SkillMetadata[] Rows)
        CaptureLoadingMetadata(RankingItem[] selected)
    {
        if (selected.Length == 0) return (null, []);
        var table = LegacyNativeNinjaFile.gouyu_type ?? throw UnavailableRanking();
        if (table.Count > MaximumEquippedMetadataRows) throw UnavailableRanking();
        var rows = new List<SkillMetadata>();
        foreach (var pair in table)
        {
            if (rows.Count >= MaximumEquippedMetadataRows || pair.Value is not { } row) throw UnavailableRanking();
            rows.Add(new(pair.Key, row, row.ItemID, row.Level, row.magic1, row.magic2));
        }
        return (table, rows.ToArray());
    }

    private static LegacyNativeSpellStorage.NinjaSkillChoice[] PrepareLoadingSkills(
        RankingItem[] selected, SkillMetadata[] metadata)
    {
        var choices = new List<LegacyNativeSpellStorage.NinjaSkillChoice>(16);
        foreach (var item in selected)
        {
            SkillMetadata? found = null;
            foreach (var row in metadata)
                if (row.Id == item.Id && row.Level == item.Level)
                {
                    if (found.HasValue) throw UnavailableRanking();
                    found = row;
                }
            if (found is not { } match || match.First > ushort.MaxValue || match.Second > ushort.MaxValue)
                throw UnavailableRanking();
            choices.Add(new((ushort)match.First, item.Level));
            choices.Add(new((ushort)match.Second, item.Level));
        }
        return choices.ToArray();
    }

    private static void RequireLoadingMetadata(Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? table,
        SkillMetadata[] rows)
    {
        if (table is null) return;
        if (!ReferenceEquals(LegacyNativeNinjaFile.gouyu_type, table) || table.Count != rows.Length)
            throw UnavailableRanking();
        var ordinal = 0;
        foreach (var pair in table)
        {
            if (ordinal >= rows.Length) throw UnavailableRanking();
            var saved = rows[ordinal++];
            if (pair.Key != saved.Key || !ReferenceEquals(pair.Value, saved.Row) || pair.Value.ItemID != saved.Id ||
                pair.Value.Level != saved.Level || pair.Value.magic1 != saved.First || pair.Value.magic2 != saved.Second)
                throw UnavailableRanking();
        }
        if (ordinal != rows.Length) throw UnavailableRanking();
    }
}
