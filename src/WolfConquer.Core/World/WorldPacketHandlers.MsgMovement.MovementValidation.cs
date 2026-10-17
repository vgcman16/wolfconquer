using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryValidateStaticTerrainLocation(
        WorldPacketHandlerContext context,
        long mapId,
        long x,
        long y,
        out LegacyGameMapTerrainLocation location)
    {
        location = default;
        if (context.Terrain is not { } terrain ||
            mapId is < int.MinValue or > int.MaxValue ||
            x is < int.MinValue or > int.MaxValue ||
            y is < int.MinValue or > int.MaxValue)
        {
            return true;
        }

        var terrainMapId = (int)mapId;
        if (!terrain.TryGetTerrain(terrainMapId, out _))
        {
            return true;
        }

        location = terrain.QueryLocation(terrainMapId, (int)x, (int)y);
        return location.IsWalkable;
    }

    private static bool TryValidateStaticTerrainSegment(
        WorldPacketHandlerContext context,
        long mapId,
        long startX,
        long startY,
        long endX,
        long endY,
        out LegacyGameMapTerrainLocation blocked)
    {
        blocked = default;
        if (context.Terrain is not { } terrain ||
            mapId is < int.MinValue or > int.MaxValue ||
            startX is < int.MinValue or > int.MaxValue ||
            startY is < int.MinValue or > int.MaxValue ||
            endX is < int.MinValue or > int.MaxValue ||
            endY is < int.MinValue or > int.MaxValue)
        {
            return true;
        }

        var terrainMapId = (int)mapId;
        if (!terrain.TryGetTerrain(terrainMapId, out _))
        {
            return true;
        }

        return !terrain.TryFindFirstBlockedSegmentLocation(
            terrainMapId,
            (int)startX,
            (int)startY,
            (int)endX,
            (int)endY,
            out blocked);
    }

    private static string FormatTerrainLocation(LegacyGameMapTerrainLocation location)
    {
        return "map=" + location.MapId.ToString(CultureInfo.InvariantCulture) +
            " x=" + location.X.ToString(CultureInfo.InvariantCulture) +
            " y=" + location.Y.ToString(CultureInfo.InvariantCulture) +
            " status=" + location.Status +
            " terrainFlag=" + FormatNullableInt16(location.TerrainFlag) +
            " surface=" + FormatNullableUInt16(location.Surface) +
            " altitude=" + FormatNullableInt16(location.Altitude);
    }

    private static string FormatNullableInt16(short? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    }

    private static string FormatNullableUInt16(ushort? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "null";
    }
}
