namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    internal static void PublishPrestigeEntry(Entry entry, bool includeClass)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (SynRoot)
        {
            var uid = entry.UID;
            var category = entry.type;
            if (uid == 0 || (uint)category is < 1 or > 11)
            {
                throw new InvalidOperationException("Prestige publication requires a valid entry identity and category.");
            }

            var sources = CapturePrestigeRefreshRegistry();
            RequirePrestigeRefreshOwner(sources, Type.World);
            if (includeClass)
            {
                RequirePrestigeRefreshOwner(sources, category);
            }

            var eligible = entry.TotalPoints >= 20_000;
            var prepared = new List<PreparedPrestigeRank>(sources.Length);
            foreach (var source in sources)
            {
                var retain = eligible && (source.Category == Type.World ||
                    (includeClass && source.Category == category));
                var change = PreparePrestigeRankChange(source, uid, retain ? entry : null);
                if (change is not null)
                {
                    prepared.Add(change);
                }
            }

            // All affected dictionaries and champions are ready. Publication only
            // stores references; the captured physical Rank owners remain in place.
            foreach (var change in prepared)
            {
                PublishPrestigeRank(change);
            }
        }
    }

    private static void RequirePrestigeRefreshOwner(PrestigeRankSource[] sources, Type category)
    {
        if (!sources.Any(source => source.Category == category))
        {
            throw new InvalidOperationException("Prestige publication requires its world and included class owners.");
        }
    }
}
