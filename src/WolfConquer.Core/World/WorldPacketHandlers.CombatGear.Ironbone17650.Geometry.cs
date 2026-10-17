namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyCombatGearIronbone17650Line(
        ushort OriginX,
        ushort OriginY,
        byte Range,
        IReadOnlyList<(int X, int Y)> Coordinates,
        ushort EndpointX,
        ushort EndpointY)
    {
        public bool Contains(long x, long y)
        {
            if (LegacyGetDistance(OriginX, OriginY, x, y) > Range)
            {
                return false;
            }

            foreach (var coordinate in Coordinates)
            {
                if (LegacyGetDistance(
                        unchecked((ushort)coordinate.X), unchecked((ushort)coordinate.Y), x, y) <= 2)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool TryCreateLegacyCombatGearIronbone17650Line(
        ushort casterX,
        ushort casterY,
        ushort queryX,
        ushort queryY,
        byte range,
        Func<ushort, ushort, bool> isValidLocation,
        out LegacyCombatGearIronbone17650Line line)
    {
        line = null!;
        if (queryX == casterX || queryX == 0)
        {
            return false;
        }

        var deltaX = (int)queryX - casterX;
        var deltaY = (int)queryY - casterY;
        var divisor = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        var stepX = (float)deltaX / divisor;
        var stepY = (float)deltaY / divisor;
        var x = (float)casterX;
        var y = (float)casterY;
        var coordinates = new List<(int X, int Y)>(range + 1)
        {
            ((int)Math.Round(x), (int)Math.Round(y)),
        };
        for (var index = 0; index < range; index++)
        {
            x += stepX;
            y += stepY;
            coordinates.Add(((int)Math.Round(x), (int)Math.Round(y)));
        }

        for (var offset = 1; offset <= Math.Min(3, coordinates.Count); offset++)
        {
            var endpoint = coordinates[^offset];
            var endpointX = unchecked((ushort)endpoint.X);
            var endpointY = unchecked((ushort)endpoint.Y);
            if (isValidLocation(endpointX, endpointY))
            {
                line = new LegacyCombatGearIronbone17650Line(
                    casterX, casterY, range, coordinates, endpointX, endpointY);
                return true;
            }
        }

        return false;
    }
}
