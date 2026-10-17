using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyGameMapTerrainQueryTests
{
    [Fact]
    public void QueryLocationReturnsWalkabilitySurfaceAndAltitudeFromParsedDMap()
    {
        var query = CreateQuery(
            1002,
            width: 2,
            height: 2,
            [
                Cell(terrainFlag: 0, surface: 10, altitude: 100),
                Cell(terrainFlag: 1, surface: 11, altitude: 101),
                Cell(terrainFlag: 0, surface: 12, altitude: -5),
                Cell(terrainFlag: -1, surface: 13, altitude: 103),
            ]);

        var walkable = query.QueryLocation(1002, 0, 0);
        Assert.Equal(LegacyGameMapTerrainLocationStatus.Walkable, walkable.Status);
        Assert.True(walkable.IsWalkable);
        Assert.True(walkable.HasTerrain);
        Assert.True(walkable.IsInBounds);
        Assert.Equal((short)0, walkable.TerrainFlag);
        Assert.Equal((ushort)10, walkable.Surface);
        Assert.Equal((short)100, walkable.Altitude);

        var blocked = query.QueryLocation(1002, 1, 1);
        Assert.Equal(LegacyGameMapTerrainLocationStatus.Blocked, blocked.Status);
        Assert.False(blocked.IsWalkable);
        Assert.True(blocked.IsInBounds);
        Assert.Equal((short)-1, blocked.TerrainFlag);
        Assert.Equal((ushort)13, blocked.Surface);
        Assert.Equal((short)103, blocked.Altitude);
    }

    [Fact]
    public void QueryLocationClassifiesMissingMapAndOutOfBoundsAsNotWalkable()
    {
        var query = CreateQuery(
            1002,
            width: 1,
            height: 1,
            [Cell(terrainFlag: 0, surface: 1, altitude: 7)]);

        var missingMap = query.QueryLocation(9999, 0, 0);
        Assert.Equal(LegacyGameMapTerrainLocationStatus.MissingMap, missingMap.Status);
        Assert.False(missingMap.IsWalkable);
        Assert.False(missingMap.HasTerrain);
        Assert.False(missingMap.IsInBounds);
        Assert.False(query.TryGetAltitude(9999, 0, 0, out _));

        var outOfBounds = query.QueryLocation(1002, 1, 0);
        Assert.Equal(LegacyGameMapTerrainLocationStatus.OutOfBounds, outOfBounds.Status);
        Assert.False(outOfBounds.IsWalkable);
        Assert.True(outOfBounds.HasTerrain);
        Assert.False(outOfBounds.IsInBounds);
        Assert.False(query.TryGetAltitude(1002, 1, 0, out _));
    }

    [Fact]
    public void SegmentWalkabilityUsesLegacyRoundedLineAndReportsFirstBlockedCell()
    {
        var query = CreateQuery(
            1002,
            width: 4,
            height: 4,
            [
                Cell(0, 0, 0), Cell(0, 0, 0), Cell(0, 0, 0), Cell(0, 0, 0),
                Cell(0, 0, 0), Cell(0, 0, 0), Cell(1, 0, 0), Cell(0, 0, 0),
                Cell(0, 0, 0), Cell(0, 0, 0), Cell(0, 0, 0), Cell(0, 0, 0),
                Cell(0, 0, 0), Cell(0, 0, 0), Cell(0, 0, 0), Cell(0, 0, 0),
            ]);

        Assert.True(query.IsSegmentWalkable(1002, 0, 0, 3, 0));
        Assert.False(query.IsSegmentWalkable(1002, 0, 0, 3, 2));

        Assert.True(query.TryFindFirstBlockedSegmentLocation(1002, 0, 0, 3, 2, out var blocked));
        Assert.Equal(2, blocked.X);
        Assert.Equal(1, blocked.Y);
        Assert.Equal(LegacyGameMapTerrainLocationStatus.Blocked, blocked.Status);
    }

    [Fact]
    public void SegmentWalkabilityCanIncludeOrIgnoreTheStartingCell()
    {
        var query = CreateQuery(
            1002,
            width: 3,
            height: 1,
            [
                Cell(1, 0, 0),
                Cell(0, 0, 0),
                Cell(0, 0, 0),
            ]);

        Assert.True(query.IsSegmentWalkable(1002, 0, 0, 2, 0));
        Assert.False(query.IsSegmentWalkable(1002, 0, 0, 2, 0, includeStart: true));
    }

    [Fact]
    public void ConstructorRejectsDuplicateMapTerrain()
    {
        var terrain = LegacyGameMapParser.ParseDMap(
            BuildDMap(1, 1, [Cell(0, 0, 0)]),
            mapId: 1002,
            sourceFile: "map1002.dmap");

        Assert.Throws<ArgumentException>(() => new LegacyGameMapTerrainQuery([terrain, terrain]));
    }

    private static LegacyGameMapTerrainQuery CreateQuery(
        int mapId,
        int width,
        int height,
        IReadOnlyList<TestDMapCell> cells)
    {
        var terrain = LegacyGameMapParser.ParseDMap(
            BuildDMap(width, height, cells),
            mapId,
            $"map{mapId}.dmap");

        return new LegacyGameMapTerrainQuery([terrain]);
    }

    private static TestDMapCell Cell(short terrainFlag, ushort surface, short altitude)
    {
        return new TestDMapCell(terrainFlag, surface, altitude);
    }

    private static byte[] BuildDMap(int width, int height, IReadOnlyList<TestDMapCell> cells)
    {
        Assert.Equal(width * height, cells.Count);

        var bytes = new byte[LegacyGameMapParser.DMapHeaderLength + (2 * sizeof(int)) + (height * ((width * 6) + sizeof(int)))];
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
