namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    internal static void PublishPrestigeEntry(Entry entry, bool includeClass)
    {
        ArgumentNullException.ThrowIfNull(entry);
        // Keep the existing low-score no-op, including its separate stale-low issue.
        if (entry.TotalPoints < 20_000)
        {
            return;
        }

        lock (SynRoot)
        {
            if (entry.UID == 0 || (uint)entry.type is < 1 or > 11 || Ranks is null)
            {
                throw new InvalidOperationException("Prestige publication requires a valid entry and ranking graph.");
            }

            var ranks = Ranks;
            var world = RequirePrestigePublicationRank(ranks, Type.World);
            var classRank = includeClass ? RequirePrestigePublicationRank(ranks, entry.type) : null;
            var preparedClass = classRank is null ? null : PreparePrestigePublication(classRank, entry);
            var preparedWorld = PreparePrestigePublication(world, entry);

            // Every lookup, validation, allocation, sort and champion selection has
            // succeeded. Only reference assignments remain under the shared gate.
            if (preparedClass is not null)
            {
                PublishPrestigeRank(preparedClass);
            }
            PublishPrestigeRank(preparedWorld);
        }
    }

    private static Rank RequirePrestigePublicationRank(
        LegacyNativeDbSafeDictionary<Type, Rank> ranks, Type category)
    {
        if (!ranks.TryGetValue(category, out var rank) || rank is null ||
            rank._Type != category || rank.Items is null || rank.MaxItems <= 0)
        {
            throw new InvalidOperationException("Prestige publication requires a complete ranking owner.");
        }

        ValidatePrestigePublicationItems(rank.Items);
        return rank;
    }

    private static void ValidatePrestigePublicationItems(IReadOnlyDictionary<uint, Entry> items)
    {
        foreach (var pair in items)
        {
            if (pair.Value is null || pair.Key == 0 || pair.Value.UID != pair.Key)
            {
                throw new InvalidOperationException("Prestige publication found an invalid ranking identity.");
            }
        }
    }

    private static PreparedPrestigeRank PreparePrestigePublication(Rank rank, Entry entry)
    {
        // Replacing an existing key keeps its prior position; adding appends it.
        // Stable OrderByDescending/Take retains valid AddItem tie/cap semantics.
        var candidates = new Dictionary<uint, Entry>(rank.Items, rank.Items.Comparer)
        {
            [entry.UID] = entry,
        };
        ValidatePrestigePublicationItems(candidates);
        var items = candidates.OrderByDescending(pair => pair.Value.TotalPoints)
            .Take(rank.MaxItems)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return new PreparedPrestigeRank(rank, items, items.Values.First());
    }

    private static void PublishPrestigeRank(PreparedPrestigeRank prepared)
    {
        prepared.Owner.Items = prepared.Items;
        prepared.Owner.BestOfClass = prepared.Champion;
    }

    private sealed record PreparedPrestigeRank(
        Rank Owner,
        Dictionary<uint, Entry> Items,
        Entry Champion);
}
