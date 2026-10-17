using System.Buffers.Binary;
using System.Text;

namespace WolfConquer.Core.Data;

public static class LegacyGameMapParser
{
    public const int DMapHeaderLength = 268;

    private const int DMapCellLength = sizeof(short) + sizeof(ushort) + sizeof(short);
    private const int DMapRowFooterLength = sizeof(int);

    public static IReadOnlyList<LegacyGameMapEntry> ParseGameMapDatFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing legacy GameMap.dat file: {path}", path);
        }

        return ParseGameMapDat(File.ReadAllBytes(path), Path.GetFileName(path));
    }

    public static IReadOnlyList<LegacyGameMapEntry> ParseGameMapDat(
        ReadOnlySpan<byte> bytes,
        string sourceFile = "gamemap.dat")
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        var offset = 0;
        var count = ReadInt32(bytes, ref offset, sourceFile, "map count");
        if (count < 0)
        {
            throw new FormatException($"Legacy GameMap.dat '{sourceFile}' has negative map count {count}.");
        }

        var entries = new LegacyGameMapEntry[count];
        for (var i = 0; i < entries.Length; i++)
        {
            var mapId = ReadInt32(bytes, ref offset, sourceFile, $"map id for entry {i}");
            var pathLength = ReadInt32(bytes, ref offset, sourceFile, $"path length for entry {i}");
            if (pathLength < 0)
            {
                throw new FormatException(
                    $"Legacy GameMap.dat '{sourceFile}' has negative path length {pathLength} for entry {i}.");
            }

            var rawMapFile = ReadAscii(bytes, ref offset, pathLength, sourceFile, $"path for entry {i}");
            var extra = ReadInt32(bytes, ref offset, sourceFile, $"extra value for entry {i}");

            entries[i] = new LegacyGameMapEntry(
                mapId,
                rawMapFile,
                rawMapFile.Replace(".7z", ".dmap"),
                extra);
        }

        if (offset != bytes.Length)
        {
            throw new FormatException(
                $"Legacy GameMap.dat '{sourceFile}' has {bytes.Length - offset} trailing bytes.");
        }

        return entries;
    }

    public static LegacyDMapTerrain ParseDMapFile(string path, int mapId = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing legacy .dmap file: {path}", path);
        }

        return ParseDMap(File.ReadAllBytes(path), mapId, Path.GetFileName(path));
    }

    public static LegacyDMapTerrain ParseDMap(
        ReadOnlySpan<byte> bytes,
        int mapId = 0,
        string sourceFile = "map.dmap")
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        var offset = DMapHeaderLength;
        if (bytes.Length < offset + (2 * sizeof(int)))
        {
            throw new FormatException($"Legacy .dmap terrain '{sourceFile}' is missing the terrain header.");
        }

        var width = ReadInt32(bytes, ref offset, sourceFile, "width");
        var height = ReadInt32(bytes, ref offset, sourceFile, "height");
        if (width < 0)
        {
            throw new FormatException($"Legacy .dmap terrain '{sourceFile}' has negative width {width}.");
        }

        if (height < 0)
        {
            throw new FormatException($"Legacy .dmap terrain '{sourceFile}' has negative height {height}.");
        }

        var cellCount = checked((long)width * height);
        if (cellCount > int.MaxValue)
        {
            throw new FormatException($"Legacy .dmap terrain '{sourceFile}' has too many cells.");
        }

        var rowLength = checked(((long)width * DMapCellLength) + DMapRowFooterLength);
        var expectedLength = checked((long)DMapHeaderLength + (2 * sizeof(int)) + ((long)height * rowLength));
        if (bytes.Length < expectedLength)
        {
            throw new FormatException($"Legacy .dmap terrain '{sourceFile}' is truncated.");
        }

        var cells = new LegacyDMapCell[(int)cellCount];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                cells[(y * width) + x] = new LegacyDMapCell(
                    x,
                    y,
                    ReadInt16(bytes, ref offset, sourceFile, $"terrain flag at {x},{y}"),
                    ReadUInt16(bytes, ref offset, sourceFile, $"surface value at {x},{y}"),
                    ReadInt16(bytes, ref offset, sourceFile, $"altitude at {x},{y}"));
            }

            _ = ReadInt32(bytes, ref offset, sourceFile, $"row footer for row {y}");
        }

        return new LegacyDMapTerrain(mapId, sourceFile, width, height, cells);
    }

    private static string ReadAscii(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        int length,
        string sourceFile,
        string fieldName)
    {
        if (bytes.Length - offset < length)
        {
            throw new FormatException($"Legacy GameMap.dat '{sourceFile}' is truncated while reading {fieldName}.");
        }

        var value = Encoding.ASCII.GetString(bytes.Slice(offset, length));
        offset += length;
        return value;
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset, string sourceFile, string fieldName)
    {
        if (bytes.Length - offset < sizeof(int))
        {
            throw new FormatException($"Legacy map binary '{sourceFile}' is truncated while reading {fieldName}.");
        }

        var value = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, sizeof(int)));
        offset += sizeof(int);
        return value;
    }

    private static short ReadInt16(ReadOnlySpan<byte> bytes, ref int offset, string sourceFile, string fieldName)
    {
        if (bytes.Length - offset < sizeof(short))
        {
            throw new FormatException($"Legacy map binary '{sourceFile}' is truncated while reading {fieldName}.");
        }

        var value = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(offset, sizeof(short)));
        offset += sizeof(short);
        return value;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, ref int offset, string sourceFile, string fieldName)
    {
        if (bytes.Length - offset < sizeof(ushort))
        {
            throw new FormatException($"Legacy map binary '{sourceFile}' is truncated while reading {fieldName}.");
        }

        var value = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));
        offset += sizeof(ushort);
        return value;
    }
}
