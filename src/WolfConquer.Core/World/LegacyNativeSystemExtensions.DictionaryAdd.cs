namespace WolfConquer.Core.World;

// Complete genuine System.Extensions.Add from the held GameServer/SystemExtensions.dll
// referenced by native MrRayzo.csproj23..24. It takes IDictionary and overwrites
// via its indexer; ConcurrentDictionary duplicate keys do not TryAdd or throw here.
// Unrelated Transform/Remove methods are absent, not replaced or stubbed.
internal static class LegacyNativeSystemExtensions
{
    public static void Add<T, T2>(this IDictionary<T, T2> dict, T key, T2 value)
    {
        dict[key] = value;
    }
}
