namespace WolfConquer.Core.Data;

public readonly record struct LegacyMapAliasResolution(
    int RequestedMapId,
    int TerrainSourceMapId)
{
    public bool IsAlias => RequestedMapId != TerrainSourceMapId;

    public int BaseMapId => TerrainSourceMapId;

    public uint LegacyGameMapBaseId => IsAlias ? checked((uint)TerrainSourceMapId) : 0;
}

public static class LegacyMapAliasResolver
{
    public static IReadOnlyList<LegacyMapAliasResolution> KnownAliases { get; } = BuildKnownAliases();

    public static LegacyMapAliasResolution Resolve(int requestedMapId)
    {
        return new LegacyMapAliasResolution(requestedMapId, ResolveTerrainSourceMapId(requestedMapId));
    }

    public static bool TryResolveAlias(int requestedMapId, out LegacyMapAliasResolution resolution)
    {
        resolution = Resolve(requestedMapId);
        return resolution.IsAlias;
    }

    public static int ResolveTerrainSourceMapId(int requestedMapId)
    {
        return requestedMapId switch
        {
            >= 3830 and <= 3834 => 1780,
            >= 3826 and <= 3829 => 3825,
            >= 44455 and <= 44457 => 10088,
            >= 44460 and <= 44463 => 10090,
            7357 => 6000,
            5040 => 5000,
            5342 => 700,
            5339 => 3851,
            2351 or 2352 or 2353 => 1351,
            6011 => 1021,
            9988 => 8709,
            8437 => 1801,
            1818 => 1765,
            1052 => 1082,
            6072 or 1782 or 1783 => 1004,
            1784 => 601,
            1794 => 1028,
            5599 => 1016,
            1792 => 1014,
            1791 => 1765,
            6784 => 1505,
            6546 => 3872,
            6521 or 8009 or 6891 => 2071,
            6525 or 8521 => 1507,
            6729 => 3825,
            10429 => 10428,
            _ => requestedMapId,
        };
    }

    private static IReadOnlyList<LegacyMapAliasResolution> BuildKnownAliases()
    {
        var aliases = new List<LegacyMapAliasResolution>();
        AddRange(aliases, 3830, 3834, 1780);
        AddRange(aliases, 3826, 3829, 3825);
        AddRange(aliases, 44455, 44457, 10088);
        AddRange(aliases, 44460, 44463, 10090);
        Add(aliases, 7357, 6000);
        Add(aliases, 5040, 5000);
        Add(aliases, 5342, 700);
        Add(aliases, 5339, 3851);
        Add(aliases, 2351, 1351);
        Add(aliases, 2352, 1351);
        Add(aliases, 2353, 1351);
        Add(aliases, 6011, 1021);
        Add(aliases, 9988, 8709);
        Add(aliases, 8437, 1801);
        Add(aliases, 1818, 1765);
        Add(aliases, 1052, 1082);
        Add(aliases, 6072, 1004);
        Add(aliases, 1782, 1004);
        Add(aliases, 1783, 1004);
        Add(aliases, 1784, 601);
        Add(aliases, 1794, 1028);
        Add(aliases, 5599, 1016);
        Add(aliases, 1792, 1014);
        Add(aliases, 1791, 1765);
        Add(aliases, 6784, 1505);
        Add(aliases, 6546, 3872);
        Add(aliases, 6521, 2071);
        Add(aliases, 8009, 2071);
        Add(aliases, 6891, 2071);
        Add(aliases, 6525, 1507);
        Add(aliases, 8521, 1507);
        Add(aliases, 6729, 3825);
        Add(aliases, 10429, 10428);
        return aliases;
    }

    private static void AddRange(List<LegacyMapAliasResolution> aliases, int start, int end, int terrainSourceMapId)
    {
        for (var mapId = start; mapId <= end; mapId++)
        {
            Add(aliases, mapId, terrainSourceMapId);
        }
    }

    private static void Add(List<LegacyMapAliasResolution> aliases, int requestedMapId, int terrainSourceMapId)
    {
        aliases.Add(new LegacyMapAliasResolution(requestedMapId, terrainSourceMapId));
    }
}
