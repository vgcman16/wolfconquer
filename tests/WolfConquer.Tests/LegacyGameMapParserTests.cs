using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class LegacyGameMapParserTests
{
    [Fact]
    public void ParseGameMapDatReadsLegacyMappingsAndNormalizesArchivePaths()
    {
        var entries = LegacyGameMapParser.ParseGameMapDat(
            BuildGameMapDat(
            [
                Entry(1002, "map/map1002.7z", 7),
                Entry(1038, "map/desert.dmap", 9),
            ]),
            "GameMap.dat");

        Assert.Equal(2, entries.Count);
        Assert.Equal(new LegacyGameMapEntry(1002, "map/map1002.7z", "map/map1002.dmap", 7), entries[0]);
        Assert.Equal(new LegacyGameMapEntry(1038, "map/desert.dmap", "map/desert.dmap", 9), entries[1]);
    }

    [Fact]
    public void ParseDMapReadsDimensionsWalkabilityAndAltitude()
    {
        var terrain = LegacyGameMapParser.ParseDMap(
            BuildDMap(
                2,
                2,
                [
                    Cell(terrainFlag: 0, surface: 10, altitude: 100),
                    Cell(terrainFlag: 1, surface: 11, altitude: 101),
                    Cell(terrainFlag: 0, surface: 12, altitude: -5),
                    Cell(terrainFlag: -1, surface: 13, altitude: 103),
                ]),
            mapId: 1002,
            sourceFile: "map/map1002.dmap");

        Assert.Equal(1002, terrain.MapId);
        Assert.Equal("map/map1002.dmap", terrain.SourceFile);
        Assert.Equal(2, terrain.Width);
        Assert.Equal(2, terrain.Height);
        Assert.Equal(4, terrain.Cells.Count);

        Assert.True(terrain.IsWalkable(0, 0));
        Assert.False(terrain.IsWalkable(1, 0));
        Assert.True(terrain.IsWalkable(0, 1));
        Assert.False(terrain.IsWalkable(1, 1));

        Assert.Equal(100, terrain.GetAltitude(0, 0));
        Assert.Equal(101, terrain.GetAltitude(1, 0));
        Assert.Equal(-5, terrain.GetAltitude(0, 1));
        Assert.Equal(103, terrain.GetAltitude(1, 1));
        Assert.Equal(13, terrain.GetCell(1, 1).Surface);
    }

    [Fact]
    public void ParseGameMapDatRejectsTruncatedMapping()
    {
        var bytes = BuildGameMapDat([Entry(1002, "map/map1002.7z", 0)]);

        Assert.Throws<FormatException>(
            () => LegacyGameMapParser.ParseGameMapDat(bytes.AsSpan(0, bytes.Length - 1), "GameMap.dat"));
    }

    [Fact]
    public void ParseDMapRejectsTruncatedTerrain()
    {
        var bytes = BuildDMap(2, 2, [Cell(0, 0, 1), Cell(0, 0, 2), Cell(0, 0, 3), Cell(0, 0, 4)]);

        Assert.Throws<FormatException>(
            () => LegacyGameMapParser.ParseDMap(bytes.AsSpan(0, bytes.Length - 1), sourceFile: "bad.dmap"));
    }

    [Fact]
    public void ParseDMapAllowsTrailingLegacySectionsAfterTerrainGrid()
    {
        var terrainBytes = BuildDMap(1, 1, [Cell(0, 0, 0)]);
        var bytes = new byte[terrainBytes.Length + 8];
        terrainBytes.CopyTo(bytes, 0);
        bytes[^8] = 0x41;
        bytes[^1] = 0x7F;

        var terrain = LegacyGameMapParser.ParseDMap(bytes, mapId: 6_000, sourceFile: "prison.dmap");

        Assert.Equal(6_000, terrain.MapId);
        Assert.Equal(1, terrain.Width);
        Assert.Equal(1, terrain.Height);
        Assert.True(terrain.IsWalkable(0, 0));
    }

    [Fact]
    public void DMapTerrainRejectsOutOfRangeCoordinates()
    {
        var terrain = LegacyGameMapParser.ParseDMap(
            BuildDMap(1, 1, [Cell(0, 0, 1)]),
            sourceFile: "one-cell.dmap");

        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.GetCell(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.GetCell(0, 1));
    }

    private static TestGameMapEntry Entry(int mapId, string mapFile, int extra)
    {
        return new TestGameMapEntry(mapId, mapFile, extra);
    }

    private static TestDMapCell Cell(short terrainFlag, ushort surface, short altitude)
    {
        return new TestDMapCell(terrainFlag, surface, altitude);
    }

    private static byte[] BuildGameMapDat(IReadOnlyList<TestGameMapEntry> entries)
    {
        var bytes = new List<byte>();
        WriteInt32(bytes, entries.Count);

        foreach (var entry in entries)
        {
            var pathBytes = Encoding.ASCII.GetBytes(entry.MapFile);
            WriteInt32(bytes, entry.MapId);
            WriteInt32(bytes, pathBytes.Length);
            bytes.AddRange(pathBytes);
            WriteInt32(bytes, entry.Extra);
        }

        return bytes.ToArray();
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

    private static void WriteInt32(List<byte> bytes, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        bytes.AddRange(buffer.ToArray());
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

    private sealed record TestGameMapEntry(int MapId, string MapFile, int Extra);

    private sealed record TestDMapCell(short TerrainFlag, ushort Surface, short Altitude);
}
