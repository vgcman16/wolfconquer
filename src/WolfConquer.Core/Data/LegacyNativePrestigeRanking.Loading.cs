using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    public static void Load()
    {
        lock (SynRoot)
        {
            var path = LegacyNativeServerConfig.DbLocation + "PrestigeRanking.txt";
            var rows = ReadPrestigeLoadRows(path);
            var prepared = PreparePrestigeLoadRanks(rows);
            // All reads, validation, disposal, and graph construction finish
            // before the single publication. Escaped previous graphs are untouched.
            Ranks = prepared;
        }
    }

    private static LegacyNativeDbSafeDictionary<Type, Rank> PreparePrestigeLoadRanks(IReadOnlyList<Entry> rows)
    {
        var prepared = new LegacyNativeDbSafeDictionary<Type, Rank>();
        // Stable saved-row order is this managed loader's deterministic tie
        // policy; no legacy Dictionary enumeration behavior is assumed.
        var eligible = rows.Where(entry => entry.TotalPoints >= 20_000)
            .OrderByDescending(entry => entry.TotalPoints)
            .ToArray();
        for (var index = 0; index < 12; index++)
        {
            var category = (Type)index;
            var rank = new Rank(category) { MaxItems = category == Type.World ? 100 : 30 };
            var candidates = category == Type.World ? eligible : eligible.Where(entry => entry.type == category);
            foreach (var entry in candidates.Take(rank.MaxItems))
            {
                rank.Items.Add(entry.UID, entry);
                rank.BestOfClass ??= entry;
            }

            prepared.Add(category, rank);
        }

        return prepared;
    }
}
