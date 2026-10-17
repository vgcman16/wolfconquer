using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyGameMapTerrainLazyCatalogTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "wolf-lazy-terrain-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void WorldDataCatalogConstructorUsesOnlyItsExactGameMapIndex()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LegacyGameMapTerrainQuery((WorldDataCatalog)null!));

        var query = new LegacyGameMapTerrainQuery(LegacyItemWorldDataFixture.Create());

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.Equal(LegacyGameMapTerrainLocationStatus.MissingMap, query.QueryLocation(1002, 0, 0).Status);
    }

    [Fact]
    public void ConstructorDoesNotParseAnyTerrainBeforeItIsRequested()
    {
        WriteDMap("map/first.dmap", Cell(0, 1, 2));
        WriteDMap("map/second.dmap", Cell(0, 3, 4));
        var parseCount = 0;
        var query = CreateQuery(
            [Entry(1002, "map/first.dmap"), Entry(1003, "map/second.dmap")],
            (path, mapId) =>
            {
                Interlocked.Increment(ref parseCount);
                return LegacyGameMapParser.ParseDMapFile(path, mapId);
            });

        Assert.Equal(0, query.Count);
        Assert.Equal(0, Volatile.Read(ref parseCount));

        Assert.True(query.TryGetTerrain(1002, out _));
        Assert.Equal(1, query.Count);
        Assert.Equal(1, Volatile.Read(ref parseCount));
    }

    [Theory]
    [InlineData("map/zone.dmap")]
    [InlineData("map\\zone.dmap")]
    public void RequestedTerrainNormalizesLegacySlashesAndExposesParsedCells(string catalogPath)
    {
        WriteDMap(
            "map/zone.dmap",
            Cell(0, 10, 100),
            Cell(1, 11, -7));
        var query = CreateQuery([Entry(1002, catalogPath)]);

        var walkable = query.QueryLocation(1002, 0, 0);
        var blocked = query.QueryLocation(1002, 1, 0);

        Assert.Equal(LegacyGameMapTerrainLocationStatus.Walkable, walkable.Status);
        Assert.Equal((ushort)10, walkable.Surface);
        Assert.Equal((short)100, walkable.Altitude);
        Assert.Equal(LegacyGameMapTerrainLocationStatus.Blocked, blocked.Status);
        Assert.Equal((ushort)11, blocked.Surface);
        Assert.Equal((short)-7, blocked.Altitude);
        Assert.True(query.TryGetTerrain(1002, out var terrain));
        Assert.Equal(1002, terrain.MapId);
        Assert.Equal("zone.dmap", terrain.SourceFile);
    }

    [Fact]
    public void MapAbsentFromExactGameMapIndexIsRejectedWithoutProbingFiles()
    {
        WriteDMap("map/present.dmap", Cell(0, 1, 2));
        var parseCount = 0;
        var query = CreateQuery(
            [Entry(1000, "map/present.dmap")],
            (path, mapId) =>
            {
                Interlocked.Increment(ref parseCount);
                return LegacyGameMapParser.ParseDMapFile(path, mapId);
            });

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.Equal(LegacyGameMapTerrainLocationStatus.MissingMap, query.QueryLocation(1002, 0, 0).Status);
        Assert.Equal(0, Volatile.Read(ref parseCount));
    }

    [Fact]
    public void MissingTerrainReturnsMissingMapAndCachesTheFailedLoad()
    {
        const string relativePath = "map/missing.dmap";
        var query = CreateQuery([Entry(1002, relativePath)]);

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.Equal(LegacyGameMapTerrainLocationStatus.MissingMap, query.QueryLocation(1002, 0, 0).Status);

        WriteDMap(relativePath, Cell(0, 1, 2));

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.Equal(0, query.Count);
    }

    [Fact]
    public void MalformedTerrainReturnsMissingMapAndCachesTheFailedParse()
    {
        const string relativePath = "map/malformed.dmap";
        var path = WriteFile(relativePath, [1, 2, 3, 4]);
        var query = CreateQuery([Entry(1002, relativePath)]);

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.False(query.IsWalkable(1002, 0, 0));
        Assert.False(query.TryGetAltitude(1002, 0, 0, out _));

        File.WriteAllBytes(path, BuildDMap(1, 1, [Cell(0, 1, 2)]));

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.Equal(0, query.Count);
    }

    [Fact]
    public void SuccessfulTerrainRemainsCachedAfterItsSourceFileIsRemoved()
    {
        var path = WriteDMap("map/cached.dmap", Cell(0, 1, 2));
        var query = CreateQuery([Entry(1002, "map/cached.dmap")]);

        Assert.True(query.TryGetTerrain(1002, out var first));
        File.Delete(path);
        Assert.True(query.TryGetTerrain(1002, out var second));

        Assert.Same(first, second);
        Assert.Equal(1, query.Count);
    }

    [Fact]
    public void RelativeTraversalOutsideSourceRootIsRejected()
    {
        var sourceRoot = Path.Combine(_rootPath, "source");
        Directory.CreateDirectory(sourceRoot);
        WriteDMapAtRoot(_rootPath, "outside.dmap", Cell(0, 1, 2));
        var query = CreateQuery(
            [Entry(1002, "../outside.dmap")],
            sourceRoot);

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.Equal(LegacyGameMapTerrainLocationStatus.MissingMap, query.QueryLocation(1002, 0, 0).Status);
    }

    [Fact]
    public void RootedPathOutsideSourceRootIsRejected()
    {
        var sourceRoot = Path.Combine(_rootPath, "source");
        Directory.CreateDirectory(sourceRoot);
        var outsidePath = WriteDMapAtRoot(_rootPath, "outside.dmap", Cell(0, 1, 2));
        var query = CreateQuery(
            [Entry(1002, outsidePath)],
            sourceRoot);

        Assert.False(query.TryGetTerrain(1002, out _));
    }

    [Fact]
    public void DescendantSymbolicLinkOutsideSourceRootIsRejectedWhenLinksAreAvailable()
    {
        var sourceRoot = Path.Combine(_rootPath, "source");
        var outsideRoot = Path.Combine(_rootPath, "outside");
        var linkPath = Path.Combine(sourceRoot, "linked");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(outsideRoot);
        WriteDMapAtRoot(outsideRoot, "escape.dmap", Cell(0, 1, 2));

        try
        {
            Directory.CreateSymbolicLink(linkPath, outsideRoot);
        }
        catch (Exception exception) when (exception is
                   IOException or
                   UnauthorizedAccessException or
                   PlatformNotSupportedException or
                   NotSupportedException)
        {
            return;
        }

        try
        {
            var query = CreateQuery(
                [Entry(1002, "linked/escape.dmap")],
                sourceRoot);

            Assert.False(query.TryGetTerrain(1002, out _));
        }
        finally
        {
            Directory.Delete(linkPath);
        }
    }

    [Fact]
    public void InvalidSourceRootReturnsMissingMapWithoutThrowing()
    {
        var query = new LegacyGameMapTerrainQuery(
            Entries(Entry(1002, "map/zone.dmap")),
            "\0");

        Assert.False(query.TryGetTerrain(1002, out _));
        Assert.Equal(LegacyGameMapTerrainLocationStatus.MissingMap, query.QueryLocation(1002, 0, 0).Status);
    }

    [Fact]
    public void ExpectedIoPathAndParserFailuresRemainFailClosed()
    {
        WriteDMap("map/failure.dmap", Cell(0, 1, 2));
        Exception[] expectedFailures =
        [
            new IOException("io"),
            new UnauthorizedAccessException("access"),
            new ArgumentException("path"),
            new NotSupportedException("path"),
            new FormatException("format"),
            new OverflowException("dimensions"),
            new InvalidDataException("data"),
            new System.Security.SecurityException("security"),
        ];

        foreach (var expectedFailure in expectedFailures)
        {
            var parseCount = 0;
            var query = CreateQuery(
                [Entry(1002, "map/failure.dmap")],
                (_, _) =>
                {
                    Interlocked.Increment(ref parseCount);
                    throw expectedFailure;
                });

            Assert.False(query.TryGetTerrain(1002, out _));
            Assert.Equal(LegacyGameMapTerrainLocationStatus.MissingMap, query.QueryLocation(1002, 0, 0).Status);
            Assert.Equal(1, Volatile.Read(ref parseCount));
        }
    }

    [Fact]
    public void ProgrammingFailureFromTerrainParserIsNotSwallowed()
    {
        WriteDMap("map/programming-error.dmap", Cell(0, 1, 2));
        var expected = new InvalidOperationException("parser defect");
        var query = CreateQuery(
            [Entry(1002, "map/programming-error.dmap")],
            (_, _) => throw expected);

        var actual = Assert.Throws<InvalidOperationException>(
            () => query.TryGetTerrain(1002, out _));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ConcurrentFirstAccessParsesOnceAndSharesTheCachedTerrain()
    {
        WriteDMap("map/concurrent.dmap", Cell(0, 1, 2));
        var parseCount = 0;
        using var start = new ManualResetEventSlim();
        using var parseStarted = new ManualResetEventSlim();
        using var releaseParser = new ManualResetEventSlim();
        var query = CreateQuery(
            [Entry(1002, "map/concurrent.dmap")],
            (path, mapId) =>
            {
                Interlocked.Increment(ref parseCount);
                parseStarted.Set();
                releaseParser.Wait(TimeSpan.FromSeconds(10));
                return LegacyGameMapParser.ParseDMapFile(path, mapId);
            });
        var requests = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                var found = query.TryGetTerrain(1002, out var terrain);
                return (found, terrain);
            }))
            .ToArray();

        start.Set();
        try
        {
            Assert.True(parseStarted.Wait(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            releaseParser.Set();
        }

        var results = await Task.WhenAll(requests);

        Assert.Equal(1, Volatile.Read(ref parseCount));
        Assert.All(results, result => Assert.True(result.found));
        Assert.All(results, result => Assert.Same(results[0].terrain, result.terrain));
        Assert.Equal(1, query.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private LegacyGameMapTerrainQuery CreateQuery(
        IReadOnlyList<LegacyGameMapEntry> entries,
        Func<string, int, LegacyDMapTerrain>? terrainParser = null)
    {
        return CreateQuery(entries, _rootPath, terrainParser);
    }

    private static LegacyGameMapTerrainQuery CreateQuery(
        IReadOnlyList<LegacyGameMapEntry> entries,
        string sourceRoot,
        Func<string, int, LegacyDMapTerrain>? terrainParser = null)
    {
        var entriesByMapId = entries.ToDictionary(entry => entry.MapId);
        return terrainParser is null
            ? new LegacyGameMapTerrainQuery(entriesByMapId, sourceRoot)
            : new LegacyGameMapTerrainQuery(entriesByMapId, sourceRoot, terrainParser);
    }

    private string WriteDMap(string relativePath, params TestDMapCell[] cells)
    {
        return WriteDMapAtRoot(_rootPath, relativePath, cells);
    }

    private static string WriteDMapAtRoot(
        string sourceRoot,
        string relativePath,
        params TestDMapCell[] cells)
    {
        return WriteFileAtRoot(sourceRoot, relativePath, BuildDMap(cells.Length, 1, cells));
    }

    private string WriteFile(string relativePath, byte[] bytes)
    {
        return WriteFileAtRoot(_rootPath, relativePath, bytes);
    }

    private static string WriteFileAtRoot(string sourceRoot, string relativePath, byte[] bytes)
    {
        var normalizedPath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var path = Path.Combine(sourceRoot, normalizedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static IReadOnlyDictionary<int, LegacyGameMapEntry> Entries(
        params LegacyGameMapEntry[] entries)
    {
        return entries.ToDictionary(entry => entry.MapId);
    }

    private static LegacyGameMapEntry Entry(int mapId, string mapFile)
    {
        return new LegacyGameMapEntry(mapId, mapFile, mapFile, 0);
    }

    private static TestDMapCell Cell(short terrainFlag, ushort surface, short altitude)
    {
        return new TestDMapCell(terrainFlag, surface, altitude);
    }

    private static byte[] BuildDMap(
        int width,
        int height,
        IReadOnlyList<TestDMapCell> cells)
    {
        Assert.Equal(width * height, cells.Count);

        var bytes = new byte[
            LegacyGameMapParser.DMapHeaderLength +
            (2 * sizeof(int)) +
            (height * ((width * 6) + sizeof(int)))];
        var offset = LegacyGameMapParser.DMapHeaderLength;
        WriteInt32(bytes, ref offset, width);
        WriteInt32(bytes, ref offset, height);

        var cellIndex = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = cells[cellIndex++];
                WriteInt16(bytes, ref offset, cell.TerrainFlag);
                WriteUInt16(bytes, ref offset, cell.Surface);
                WriteInt16(bytes, ref offset, cell.Altitude);
            }

            WriteInt32(bytes, ref offset, y + 1);
        }

        return bytes;
    }

    private static void WriteInt32(byte[] bytes, ref int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), value);
        offset += sizeof(int);
    }

    private static void WriteInt16(byte[] bytes, ref int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, sizeof(short)), value);
        offset += sizeof(short);
    }

    private static void WriteUInt16(byte[] bytes, ref int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
        offset += sizeof(ushort);
    }

    private sealed record TestDMapCell(short TerrainFlag, ushort Surface, short Altitude);
}
