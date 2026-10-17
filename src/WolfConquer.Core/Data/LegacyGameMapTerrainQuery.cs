using System.Collections.Concurrent;

namespace WolfConquer.Core.Data;

public enum LegacyGameMapTerrainLocationStatus
{
    MissingMap,
    OutOfBounds,
    Blocked,
    Walkable,
}

public readonly record struct LegacyGameMapTerrainLocation(
    int MapId,
    int X,
    int Y,
    LegacyGameMapTerrainLocationStatus Status,
    short? TerrainFlag = null,
    ushort? Surface = null,
    short? Altitude = null)
{
    public bool IsWalkable => Status == LegacyGameMapTerrainLocationStatus.Walkable;

    public bool HasTerrain => Status != LegacyGameMapTerrainLocationStatus.MissingMap;

    public bool IsInBounds => Status is
        LegacyGameMapTerrainLocationStatus.Walkable or
        LegacyGameMapTerrainLocationStatus.Blocked;
}

public sealed class LegacyGameMapTerrainQuery
{
    public static LegacyGameMapTerrainQuery Empty { get; } = new([]);

    private readonly ConcurrentDictionary<int, LegacyDMapTerrain> _terrainByMapId;
    private readonly IReadOnlyDictionary<int, LegacyGameMapEntry>? _lazyMapEntriesByMapId;
    private readonly ConcurrentDictionary<int, Lazy<LegacyDMapTerrain?>>? _lazyTerrainByMapId;
    private readonly Func<string, int, LegacyDMapTerrain>? _terrainParser;
    private readonly string? _sourceRoot;

    public LegacyGameMapTerrainQuery(IEnumerable<LegacyDMapTerrain> terrains)
    {
        ArgumentNullException.ThrowIfNull(terrains);

        var terrainByMapId = new ConcurrentDictionary<int, LegacyDMapTerrain>();
        foreach (var terrain in terrains)
        {
            ArgumentNullException.ThrowIfNull(terrain);
            if (!terrainByMapId.TryAdd(terrain.MapId, terrain))
            {
                throw new ArgumentException(
                    $"Duplicate legacy .dmap terrain for map {terrain.MapId}.",
                    nameof(terrains));
            }
        }

        _terrainByMapId = terrainByMapId;
    }

    public LegacyGameMapTerrainQuery(WorldDataCatalog worldData)
        : this(
            GetLegacyGameMapsByMapId(worldData),
            GetLegacyMapSourcePath(worldData),
            LegacyGameMapParser.ParseDMapFile)
    {
    }

    public LegacyGameMapTerrainQuery(
        IReadOnlyDictionary<int, LegacyGameMapEntry> legacyGameMapsByMapId,
        string sourcePath)
        : this(
            legacyGameMapsByMapId,
            sourcePath,
            LegacyGameMapParser.ParseDMapFile)
    {
    }

    internal LegacyGameMapTerrainQuery(
        IReadOnlyDictionary<int, LegacyGameMapEntry> legacyGameMapsByMapId,
        string sourcePath,
        Func<string, int, LegacyDMapTerrain> terrainParser)
    {
        ArgumentNullException.ThrowIfNull(legacyGameMapsByMapId);
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(terrainParser);

        var exactMapEntries = new Dictionary<int, LegacyGameMapEntry>();
        foreach (var (mapId, entry) in legacyGameMapsByMapId)
        {
            if (entry is not null && entry.MapId == mapId)
            {
                exactMapEntries.Add(mapId, entry);
            }
        }

        _terrainByMapId = new ConcurrentDictionary<int, LegacyDMapTerrain>();
        _lazyMapEntriesByMapId = exactMapEntries;
        _lazyTerrainByMapId = new ConcurrentDictionary<int, Lazy<LegacyDMapTerrain?>>();
        _terrainParser = terrainParser;
        _sourceRoot = TryNormalizeSourceRoot(sourcePath);
    }

    public int Count => _terrainByMapId.Count;

    public bool TryGetTerrain(int mapId, out LegacyDMapTerrain terrain)
    {
        if (_terrainByMapId.TryGetValue(mapId, out terrain!))
        {
            return true;
        }

        if (_lazyMapEntriesByMapId is null ||
            !_lazyMapEntriesByMapId.TryGetValue(mapId, out var mapEntry))
        {
            terrain = null!;
            return false;
        }

        var lazyTerrain = _lazyTerrainByMapId!.GetOrAdd(
            mapId,
            _ => new Lazy<LegacyDMapTerrain?>(
                () => TryLoadTerrain(mapEntry),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var loadedTerrain = lazyTerrain.Value;
        if (loadedTerrain is null)
        {
            terrain = null!;
            return false;
        }

        _terrainByMapId.TryAdd(mapId, loadedTerrain);
        terrain = loadedTerrain;
        return true;
    }

    public LegacyGameMapTerrainLocation QueryLocation(int mapId, int x, int y)
    {
        if (!TryGetTerrain(mapId, out var terrain))
        {
            return new LegacyGameMapTerrainLocation(
                mapId,
                x,
                y,
                LegacyGameMapTerrainLocationStatus.MissingMap);
        }

        if ((uint)x >= (uint)terrain.Width ||
            (uint)y >= (uint)terrain.Height)
        {
            return new LegacyGameMapTerrainLocation(
                mapId,
                x,
                y,
                LegacyGameMapTerrainLocationStatus.OutOfBounds);
        }

        var cell = terrain.GetCell(x, y);
        return new LegacyGameMapTerrainLocation(
            mapId,
            x,
            y,
            cell.IsWalkable
                ? LegacyGameMapTerrainLocationStatus.Walkable
                : LegacyGameMapTerrainLocationStatus.Blocked,
            cell.TerrainFlag,
            cell.Surface,
            cell.Altitude);
    }

    public bool IsWalkable(int mapId, int x, int y)
    {
        return QueryLocation(mapId, x, y).IsWalkable;
    }

    public bool TryGetAltitude(int mapId, int x, int y, out short altitude)
    {
        var location = QueryLocation(mapId, x, y);
        if (location.Altitude is not { } value)
        {
            altitude = 0;
            return false;
        }

        altitude = value;
        return true;
    }

    public bool IsSegmentWalkable(
        int mapId,
        int startX,
        int startY,
        int endX,
        int endY,
        bool includeStart = false)
    {
        return !TryFindFirstBlockedSegmentLocation(
            mapId,
            startX,
            startY,
            endX,
            endY,
            out _,
            includeStart);
    }

    public bool TryFindFirstBlockedSegmentLocation(
        int mapId,
        int startX,
        int startY,
        int endX,
        int endY,
        out LegacyGameMapTerrainLocation blocked,
        bool includeStart = false)
    {
        var first = true;
        var sameLocation = startX == endX && startY == endY;
        foreach (var (x, y) in EnumerateLegacySegmentCoordinates(startX, startY, endX, endY))
        {
            if (first)
            {
                first = false;
                if (!includeStart && !sameLocation)
                {
                    continue;
                }
            }

            var location = QueryLocation(mapId, x, y);
            if (!location.IsWalkable)
            {
                blocked = location;
                return true;
            }
        }

        blocked = default;
        return false;
    }

    private LegacyDMapTerrain? TryLoadTerrain(LegacyGameMapEntry mapEntry)
    {
        try
        {
            if (!TryResolveTerrainPath(mapEntry.MapFile, out var terrainPath))
            {
                return null;
            }

            var terrain = _terrainParser!(terrainPath, mapEntry.MapId);
            return terrain.MapId == mapEntry.MapId ? terrain : null;
        }
        catch (Exception exception) when (IsExpectedTerrainLoadFailure(exception))
        {
            // The matching source treats any failed on-demand map load as an unavailable map.
            return null;
        }
    }

    private bool TryResolveTerrainPath(string mapFile, out string terrainPath)
    {
        terrainPath = string.Empty;
        if (_sourceRoot is null || string.IsNullOrWhiteSpace(mapFile))
        {
            return false;
        }

        var normalizedMapFile = mapFile
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedMapFile))
        {
            return false;
        }

        var candidatePath = Path.GetFullPath(Path.Combine(_sourceRoot, normalizedMapFile));
        if (!IsPathWithinSourceRoot(_sourceRoot, candidatePath) ||
            !File.Exists(candidatePath) ||
            ContainsDescendantReparsePoint(_sourceRoot, candidatePath))
        {
            return false;
        }

        terrainPath = candidatePath;
        return true;
    }

    private static bool IsPathWithinSourceRoot(string sourceRoot, string candidatePath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(sourceRoot, candidatePath, comparison))
        {
            return true;
        }

        var sourceRootPrefix = Path.EndsInDirectorySeparator(sourceRoot)
            ? sourceRoot
            : sourceRoot + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(sourceRootPrefix, comparison);
    }

    private static bool ContainsDescendantReparsePoint(string sourceRoot, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, candidatePath);
        var currentPath = sourceRoot;
        foreach (var pathPart in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (pathPart == ".")
            {
                continue;
            }

            currentPath = Path.Combine(currentPath, pathPart);
            if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string? TryNormalizeSourceRoot(string sourcePath)
    {
        try
        {
            return string.IsNullOrWhiteSpace(sourcePath)
                ? null
                : Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourcePath));
        }
        catch (Exception exception) when (IsExpectedSourcePathFailure(exception))
        {
            return null;
        }
    }

    private static bool IsExpectedTerrainLoadFailure(Exception exception)
    {
        return exception is
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            FormatException or
            OverflowException or
            InvalidDataException or
            System.Security.SecurityException;
    }

    private static bool IsExpectedSourcePathFailure(Exception exception)
    {
        return exception is
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException;
    }

    private static IReadOnlyDictionary<int, LegacyGameMapEntry> GetLegacyGameMapsByMapId(
        WorldDataCatalog worldData)
    {
        ArgumentNullException.ThrowIfNull(worldData);
        return worldData.LegacyGameMapsByMapId;
    }

    private static string GetLegacyMapSourcePath(WorldDataCatalog worldData)
    {
        ArgumentNullException.ThrowIfNull(worldData);
        return worldData.Manifest.SourcePath;
    }

    private static IEnumerable<(int X, int Y)> EnumerateLegacySegmentCoordinates(
        int startX,
        int startY,
        int endX,
        int endY)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));

        yield return (startX, startY);
        if (steps == 0)
        {
            yield break;
        }

        var currentX = (double)startX;
        var currentY = (double)startY;
        var stepX = (double)deltaX / steps;
        var stepY = (double)deltaY / steps;
        for (var i = 0; i < steps; i++)
        {
            currentX += stepX;
            currentY += stepY;
            yield return ((int)Math.Round(currentX), (int)Math.Round(currentY));
        }
    }
}
