namespace WolfConquer.Core.World;

// Scoped translation of Framework OrderByDescending(...).ToArray for arena ranks.
// Framework evaluates every snapshot key once, even a singleton; net9 may skip it.
// Owned Framework/x86 default + optimized controls:18/18 in each, including faults.
internal static class LegacyNativeArenaRankingOrder
{
    internal static T[] Descending<T>(T[] source, Func<T, uint> keySelector)
    {
        var keys = new uint[source.Length];
        for (int index = 0; index < source.Length; index++)
            keys[index] = keySelector(source[index]);

        // LINQ's stable index ordering retains source references and tie order;
        // sorting never rereads a mutable native Info record.
        return Enumerable.Range(0, source.Length)
            .OrderByDescending(index => keys[index])
            .Select(index => source[index]).ToArray();
    }
}
