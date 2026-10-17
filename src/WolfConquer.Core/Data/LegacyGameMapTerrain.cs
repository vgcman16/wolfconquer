namespace WolfConquer.Core.Data;

public sealed record LegacyGameMapEntry(
    int MapId,
    string RawMapFile,
    string MapFile,
    int Extra);

public sealed record LegacyDMapTerrain(
    int MapId,
    string SourceFile,
    int Width,
    int Height,
    IReadOnlyList<LegacyDMapCell> Cells)
{
    public LegacyDMapCell GetCell(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return Cells[(y * Width) + x];
    }

    public bool IsWalkable(int x, int y)
    {
        return GetCell(x, y).IsWalkable;
    }

    public short GetAltitude(int x, int y)
    {
        return GetCell(x, y).Altitude;
    }
}

public sealed record LegacyDMapCell(
    int X,
    int Y,
    short TerrainFlag,
    ushort Surface,
    short Altitude)
{
    public bool IsWalkable => TerrainFlag == 0;
}
