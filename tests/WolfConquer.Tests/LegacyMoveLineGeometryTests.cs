using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMoveLineGeometryTests
{
    [Fact]
    public void SelectCoordinatesBuildsLegacyTwelveCellLineAndLanding()
    {
        var selection = LegacyMoveLineGeometry.SelectCoordinates(
            startX: 830,
            startY: 584,
            targetX: 842,
            targetY: 584);

        Assert.Equal(13, selection.Coordinates.Count);
        Assert.Equal(
            Enumerable.Range(830, 13)
                .Select(x => new LegacyMoveLineCoordinate((ushort)x, 584))
                .ToArray(),
            selection.Coordinates.ToArray());
        Assert.Equal(new LegacyMoveLineCoordinate(842, 584), selection.Landing);
    }

    [Fact]
    public void SelectCoordinatesStopsBeforeFirstInvalidLocation()
    {
        var selection = LegacyMoveLineGeometry.SelectCoordinates(
            startX: 830,
            startY: 584,
            targetX: 842,
            targetY: 584,
            isValidLocation: (x, _) => x < 836);

        Assert.Equal(
            Enumerable.Range(830, 6)
                .Select(x => new LegacyMoveLineCoordinate((ushort)x, 584))
                .ToArray(),
            selection.Coordinates.ToArray());
        Assert.Equal(new LegacyMoveLineCoordinate(835, 584), selection.Landing);
    }

    [Fact]
    public void SelectCoordinatesEndpointGateRejectsFinalCoordinateKeepsBuiltLineAndStartLanding()
    {
        var selection = LegacyMoveLineGeometry.SelectCoordinates(
            startX: 830,
            startY: 584,
            targetX: 842,
            targetY: 584,
            isFinalEndpointAllowed: (_, _) => false);

        Assert.Equal(
            Enumerable.Range(830, 13)
                .Select(x => new LegacyMoveLineCoordinate((ushort)x, 584))
                .ToArray(),
            selection.Coordinates.ToArray());
        Assert.Equal(new LegacyMoveLineCoordinate(830, 584), selection.Landing);
    }

    [Fact]
    public void SelectCoordinatesEndpointGateReceivesFinalBuiltCoordinateAfterInvalidLocationTruncation()
    {
        var endpointChecks = new List<LegacyMoveLineCoordinate>();

        var selection = LegacyMoveLineGeometry.SelectCoordinates(
            startX: 830,
            startY: 584,
            targetX: 842,
            targetY: 584,
            isValidLocation: (x, _) => x < 836,
            isFinalEndpointAllowed: (x, y) =>
            {
                endpointChecks.Add(new LegacyMoveLineCoordinate(x, y));
                return true;
            });

        Assert.Equal(
            Enumerable.Range(830, 6)
                .Select(x => new LegacyMoveLineCoordinate((ushort)x, 584))
                .ToArray(),
            selection.Coordinates.ToArray());
        Assert.Equal(new LegacyMoveLineCoordinate(835, 584), selection.Landing);
        Assert.NotEmpty(endpointChecks);
        Assert.All(
            endpointChecks,
            endpoint => Assert.Equal(new LegacyMoveLineCoordinate(835, 584), endpoint));
    }

    [Fact]
    public void SelectCoordinatesRoundsDiagonalLegacySteps()
    {
        var selection = LegacyMoveLineGeometry.SelectCoordinates(
            startX: 100,
            startY: 100,
            targetX: 112,
            targetY: 105);

        Assert.Equal(
            new[]
            {
                new LegacyMoveLineCoordinate(100, 100),
                new LegacyMoveLineCoordinate(101, 100),
                new LegacyMoveLineCoordinate(102, 101),
                new LegacyMoveLineCoordinate(103, 101),
                new LegacyMoveLineCoordinate(104, 102),
                new LegacyMoveLineCoordinate(105, 102),
                new LegacyMoveLineCoordinate(106, 102),
                new LegacyMoveLineCoordinate(107, 103),
                new LegacyMoveLineCoordinate(108, 103),
                new LegacyMoveLineCoordinate(109, 104),
                new LegacyMoveLineCoordinate(110, 104),
                new LegacyMoveLineCoordinate(111, 105),
                new LegacyMoveLineCoordinate(112, 105),
            },
            selection.Coordinates.ToArray());
        Assert.Equal(new LegacyMoveLineCoordinate(112, 105), selection.Landing);
    }

    [Fact]
    public void IsWithinHitRadiusUsesLegacyEuclideanRadius()
    {
        var selection = LegacyMoveLineGeometry.SelectCoordinates(
            startX: 830,
            startY: 584,
            targetX: 842,
            targetY: 584);

        Assert.True(LegacyMoveLineGeometry.IsWithinHitRadius(selection.Coordinates, 836, 585));
        Assert.False(LegacyMoveLineGeometry.IsWithinHitRadius(selection.Coordinates, 836, 586));
    }
}
