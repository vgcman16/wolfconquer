namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private static PrestigeRankSource CapturePrestigeRankSource(Rank? rank)
    {
        if (rank is null || (uint)rank._Type >= (uint)Type.Count ||
            rank.MaxItems <= 0 || rank.Items is null)
        {
            throw new InvalidOperationException("Prestige mutation requires a valid rank kind, positive limit and real items.");
        }

        var category = rank._Type;
        var maxItems = rank.MaxItems;
        var items = rank.Items;
        var rows = items.ToArray();
        var identities = new HashSet<uint>();
        foreach (var row in rows)
        {
            if (row.Value is null || row.Key == 0 || row.Value.UID != row.Key || !identities.Add(row.Key))
            {
                throw new InvalidOperationException("Prestige mutation found an invalid or duplicate ranking identity.");
            }
        }

        if (!ReferenceEquals(rank.Items, items) || rank._Type != category ||
            rank.MaxItems != maxItems || items.Count != rows.Length)
        {
            throw new InvalidOperationException("The physical prestige rank changed during admission.");
        }

        return new PrestigeRankSource(rank, category, items, rows, maxItems, rank.BestOfClass);
    }

    private static PreparedPrestigeRank? PreparePrestigeRankChange(
        PrestigeRankSource source, uint uid, Entry? replacement)
    {
        if (uid == 0 || replacement is not null && replacement.UID != uid)
        {
            throw new InvalidOperationException("Prestige mutation requires a positive matching character UID.");
        }

        var present = source.Rows.Any(row => row.Key == uid);
        if (replacement is null && !present && source.Champion?.UID != uid)
        {
            return null;
        }

        var candidates = new List<KeyValuePair<uint, Entry>>();
        foreach (var row in source.Rows)
        {
            if (row.Key != uid)
            {
                candidates.Add(row);
            }
            else if (replacement is not null)
            {
                // Replace at the original position; removing then appending
                // would change equal-score order for an existing UID.
                candidates.Add(new KeyValuePair<uint, Entry>(uid, replacement));
            }
        }

        if (replacement is not null && !present)
        {
            candidates.Add(new KeyValuePair<uint, Entry>(uid, replacement));
        }

        var retained = candidates.OrderByDescending(row => row.Value.TotalPoints)
            .Take(source.MaxItems).ToArray();
        var items = new Dictionary<uint, Entry>(retained.Length, source.Items.Comparer);
        foreach (var row in retained)
        {
            items.Add(row.Key, row.Value);
        }

        var champion = retained.Length == 0 ? null : retained[0].Value;
        return new PreparedPrestigeRank(source.Owner, items, champion);
    }

    private static void PublishPrestigeRank(PreparedPrestigeRank prepared)
    {
        prepared.Owner.Items = prepared.Items;
        prepared.Owner.BestOfClass = prepared.Champion;
    }

    private sealed record PrestigeRankSource(
        Rank Owner,
        Type Category,
        Dictionary<uint, Entry> Items,
        KeyValuePair<uint, Entry>[] Rows,
        int MaxItems,
        Entry? Champion);

    private sealed record PreparedPrestigeRank(
        Rank Owner,
        Dictionary<uint, Entry> Items,
        Entry? Champion);
}
