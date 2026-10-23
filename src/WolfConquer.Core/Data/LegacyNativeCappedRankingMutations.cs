namespace WolfConquer.Core.Data;

// Shared dictionary algorithms for the genuine Ninja/HW Rank owners. Gate and
// dictionary are their existing physical fields; selectors read typed fields only.
internal static class LegacyNativeCappedRankingMutations
{
    internal static void Remove<T>(object gate, ref Dictionary<uint, T> entries, uint uid,
        ref int limit, Func<T, uint> readPoints) where T : class
    {
        lock (gate)
        {
            if (entries.Remove(uid)) entries = Trim(entries, ref limit, readPoints);
        }
    }

    internal static void Update<T>(object gate, ref Dictionary<uint, T> entries, T item,
        ref int limit, Func<T, uint> readUid, Func<T, uint> readPoints) where T : class
    {
        lock (gate)
        {
            uint uid = readUid(item);
            entries[uid] = item;
            entries = Trim(entries, ref limit, readPoints);
        }
    }

    private static Dictionary<uint, T> Trim<T>(Dictionary<uint, T> entries,
        ref int limit, Func<T, uint> readPoints) where T : class
    {
        var ordered = entries.OrderByDescending(pair => readPoints(pair.Value));
        // Read the real MaxItems after constructing the ordered query, matching
        // the original OrderByDescending(...).Take(MaxItems) evaluation phase.
        return ordered.Take(limit).ToDictionary(static pair => pair.Key, static pair => pair.Value);
    }
}
