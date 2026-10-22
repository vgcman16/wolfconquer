namespace WolfConquer.Core.World;

// Complete genuine System.Extensions.Add/Remove from held GameServer/SystemExtensions.dll,
// referenced by native MrRayzo.csproj23..24. Preserve its actual IDictionary dispatch.
// Unrelated Transform is absent, not replaced or stubbed.
internal static class LegacyNativeSystemExtensions
{
    public static void Add<T, T2>(this IDictionary<T, T2> dict, T key, T2 value)
    {
        dict[key] = value;
    }

    public static void Remove<T, T2>(this IDictionary<T, T2> dict, T key)
    {
        dict.Remove(key);
    }
}
