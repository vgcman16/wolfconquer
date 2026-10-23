namespace WolfConquer.Core.World;

// Conditional, owner-scoped full snapshot ordering. No arbitrary key/result provider.
// Owned Framework4/x86 default + optimized and modern candidate:52/52 scoped observations.
// Array key evaluation/order only, not MapPlayers, client birth or concurrent mutation.
internal static class LegacyNativeDeathTournamentOrdering
{
    internal static LegacyNativeGameClientRuntime[] KillerElite(LegacyNativeGameClientRuntime[] source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        uint[] keys = new uint[source.Length];
        for (int i = 0; i < source.Length; i++) keys[i] = source[i].Player.KillerPkPoints;
        return Enumerable.Range(0, source.Length).OrderByDescending(i => keys[i]).Select(i => source[i]).ToArray();
    }
    internal static LegacyNativeGameClientRuntime[] ExtremePk(LegacyNativeGameClientRuntime[] source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        uint[] keys = new uint[source.Length];
        for (int i = 0; i < source.Length; i++) keys[i] = source[i].Player.XtremePkPoints;
        return Enumerable.Range(0, source.Length).OrderByDescending(i => keys[i]).Select(i => source[i]).ToArray();
    }
    internal static Dictionary<uint, LegacyNativeGameClientRuntime>[] TeamCounts(Dictionary<uint, LegacyNativeGameClientRuntime>[] source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        int[] keys = new int[source.Length];
        for (int i = 0; i < source.Length; i++) keys[i] = source[i].Count;
        return Enumerable.Range(0, source.Length).OrderByDescending(i => keys[i]).Select(i => source[i]).ToArray();
    }
}
