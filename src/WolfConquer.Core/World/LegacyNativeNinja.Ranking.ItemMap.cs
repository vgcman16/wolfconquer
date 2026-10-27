using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private static ConcurrentDictionary<uint, Item> PrepareRankingItemMap(
        ConcurrentDictionary<uint, Item> original, RankingItem[] rows, Item added)
    {
        if (added.ItemID == 0) throw UnavailableRanking();
        var expected = rows.ToDictionary(row => row.Key, row => row.Row);
        var raw = expected.TryGetValue(added.ItemID, out var previous);
        var present = original.TryGetValue(added.ItemID, out var found);
        if (present != raw || present && !ReferenceEquals(found, previous)) throw UnavailableRanking();
        RequireCurrentRankingItems(original, rows);
        var next = new ConcurrentDictionary<uint, Item>(rows.Select(row =>
            new KeyValuePair<uint, Item>(row.Key, row.Row)), original.Comparer);
        next[added.ItemID] = added;
        expected[added.ItemID] = added;
        if (next.Count > MaximumEquippedItemRows || next.Count != expected.Count) throw UnavailableRanking();
        foreach (var pair in next)
            if (!expected.TryGetValue(pair.Key, out var row) || !ReferenceEquals(pair.Value, row))
                throw UnavailableRanking();
        RequireCurrentRankingItems(original, rows); // Last comparer callout preceded this plain fence.
        return next;
    }
}
