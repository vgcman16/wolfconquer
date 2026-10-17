namespace WolfConquer.Core.World;

public readonly record struct LegacyMoveLineCoordinate(ushort X, ushort Y);

public readonly record struct LegacyMoveLineSelection(
    IReadOnlyList<LegacyMoveLineCoordinate> Coordinates,
    LegacyMoveLineCoordinate Landing);

public static class LegacyMoveLineGeometry
{
    public const int LegacyDistance = 12;
    public const double LegacyHitRadius = 1.5;

    public static LegacyMoveLineSelection SelectCoordinates(
        ushort startX,
        ushort startY,
        ushort targetX,
        ushort targetY,
        Func<ushort, ushort, bool>? isValidLocation = null,
        int distance = LegacyDistance,
        Func<ushort, ushort, bool>? isFinalEndpointAllowed = null)
    {
        var coordinates = BuildCoordinates(startX, startY, targetX, targetY, isValidLocation, distance);
        var finalCoordinate = coordinates[coordinates.Count - 1];
        var finalEndpointAllowed = isFinalEndpointAllowed?.Invoke(finalCoordinate.X, finalCoordinate.Y) ?? true;
        var landing = new LegacyMoveLineCoordinate(startX, startY);
        foreach (var coordinate in coordinates)
        {
            if (isValidLocation is not null && !isValidLocation(coordinate.X, coordinate.Y))
            {
                break;
            }

            if (!finalEndpointAllowed)
            {
                break;
            }

            landing = coordinate;
        }

        return new LegacyMoveLineSelection(coordinates, landing);
    }

    public static bool IsWithinHitRadius(
        IReadOnlyList<LegacyMoveLineCoordinate> coordinates,
        long targetX,
        long targetY,
        double radius = LegacyHitRadius)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        foreach (var coordinate in coordinates)
        {
            var deltaX = targetX - coordinate.X;
            var deltaY = targetY - coordinate.Y;
            if (Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<LegacyMoveLineCoordinate> BuildCoordinates(
        ushort startX,
        ushort startY,
        ushort targetX,
        ushort targetY,
        Func<ushort, ushort, bool>? isValidLocation,
        int distance)
    {
        if (distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        var deltaX = targetX - startX;
        var deltaY = targetY - startY;
        var steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));

        var coordinates = new List<LegacyMoveLineCoordinate>(distance + 1)
        {
            new(startX, startY),
        };

        if (steps == 0)
        {
            return coordinates;
        }

        var currentX = (float)startX;
        var currentY = (float)startY;
        var stepX = (float)deltaX / steps;
        var stepY = (float)deltaY / steps;
        for (var index = 0; index < distance; index++)
        {
            currentX += stepX;
            currentY += stepY;
            var x = checked((ushort)Math.Round(currentX));
            var y = checked((ushort)Math.Round(currentY));
            if (isValidLocation is not null && !isValidLocation(x, y))
            {
                break;
            }

            coordinates.Add(new LegacyMoveLineCoordinate(x, y));
        }

        return coordinates;
    }
}
