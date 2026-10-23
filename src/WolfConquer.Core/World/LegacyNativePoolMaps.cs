namespace WolfConquer.Core.World;

// Native Pool98 physical owner, implicit null. Native startup—not lookup—owns
// allocation/publication; no ordinary Dictionary or synthesized map is substituted.
internal static class LegacyNativePoolMaps
{
    public static LegacyNativeMapDictionary<uint, LegacyNativeGameMap>? ServerMaps;
}
