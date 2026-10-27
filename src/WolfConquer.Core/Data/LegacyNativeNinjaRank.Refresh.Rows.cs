namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    private static Dictionary<uint, Entry>? PrepareMap(RankSource source, uint uid, Entry? replacement)
    {
        var found = false;
        var rows = new List<RankRow>();
        foreach (var row in source.Rows)
        {
            if (row.Uid != uid) { rows.Add(row); continue; }
            found = true;
            if (replacement is not null)
                rows.Add(new(uid, replacement, uid, source.Category, replacement.TotalPoints));
        }
        if (!found && replacement is null) return null;
        if (!found && replacement is not null)
            rows.Add(new(uid, replacement, uid, source.Category, replacement.TotalPoints));

        // Stable ordering retains an existing UID's key position on replacement.
        // Only retained candidates are available; no discarded-row backfill exists.
        return rows.OrderByDescending(row => row.Points).Take(source.Limit)
            .ToDictionary(row => row.Key, row => row.Row);
    }
}
