using WolfConquer.Core.World;
using Cell = WolfConquer.Core.World.LegacyNativeMapFlagType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapLoadingTests
{
    [Fact]
    public void GroundPlacementAcceptsExistingFreeCellWithoutMovingOrDroppingValidBit()
    {
        var map = Map(9, 9);
        ushort x = 4, y = 4;
        Assert.True(map.AddGroundItem(ref x, ref y));
        Assert.Equal((ushort)4, x); Assert.Equal((ushort)4, y);
        Assert.Equal(Cell.Valid | Cell.Item, map.cells[x, y]);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void OccupiedGroundPlacementSearchesSourceYThenXRange(int range, int expected)
    {
        var map = Map(12, 12);
        map.cells[4, 4] |= Cell.Item;
        ushort x = 4, y = 4;
        Assert.True(map.AddGroundItem(ref x, ref y, (byte)range));
        Assert.Equal((ushort)expected, x);
        Assert.Equal((ushort)expected, y);
        Assert.Equal(Cell.Valid | Cell.Item, map.cells[x, y]);
        Assert.Equal(Cell.Valid | Cell.Item, map.cells[4, 4]);
    }

    [Fact]
    public void FailedGroundPlacementSetsBothReferencesZeroWithoutAddingCellFlag()
    {
        var map = Map(9, 9, fill: Cell.None);
        ushort x = 4, y = 4;
        Assert.False(map.AddGroundItem(ref x, ref y));
        Assert.Equal((ushort)0, x); Assert.Equal((ushort)0, y);
        Assert.All(map.cells.Cast<Cell>(), value => Assert.Equal(Cell.None, value));
    }

    [Fact]
    public void GuildTeleporterPlacementRetainsSourceCounterintuitiveInvalidCenterAdmission()
    {
        var map = Map(20, 20, fill: Cell.None);
        ushort x = 10, y = 10;
        Assert.True(map.AddGuildTeleporterItem(ref x, ref y));
        Assert.Equal((ushort)10, x); Assert.Equal((ushort)10, y);
        Assert.Equal(Cell.Item, map.cells[10, 10]);
    }

    [Fact]
    public void GuildTeleporterValidCenterScansSixTileRangeAndMovesToFirstInteriorCandidate()
    {
        var map = Map(20, 20);
        ushort x = 10, y = 10;
        Assert.True(map.AddGuildTeleporterItem(ref x, ref y));
        Assert.Equal((ushort)4, x); Assert.Equal((ushort)4, y);
        Assert.Equal(Cell.Valid | Cell.Item, map.cells[4, 4]);
        Assert.Equal(Cell.Valid, map.cells[10, 10]);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(2, -1, 0)]
    [InlineData(3, -1, -1)]
    [InlineData(4, 0, -1)]
    [InlineData(5, 1, -1)]
    [InlineData(6, 1, 0)]
    [InlineData(7, 1, 1)]
    public void PushbackRetainsAllNativeAngleSteps(int angle, int dx, int dy)
    {
        // Enum0=SouthWest,1=West,2=NorthWest,3=North,4=NorthEast,
        //5=East,6=SouthEast,7=South.
        var map = Map(32, 32);
        uint x = 10, y = 10;
        map.Pushback(ref x, ref y, (LegacyNativePlayerMapFlags.ConquerAngle)angle, 2);
        Assert.Equal((uint)(10 + dx * 2), x);
        Assert.Equal((uint)(10 + dy * 2), y);
    }

    [Fact]
    public void PushbackStopsAtBlockedOrWrappedBoundaryAndAcceptsNpcTile()
    {
        var map = Map(32, 32);
        map.cells[11, 9] = Cell.Npc;
        map.cells[12, 8] = Cell.None;
        uint x = 10, y = 10;
        map.Pushback(ref x, ref y, LegacyNativePlayerMapFlags.ConquerAngle.East, 10);
        Assert.Equal(11u, x); Assert.Equal(9u, y);
        x = 0; y = 0;
        map.Pushback(ref x, ref y, LegacyNativePlayerMapFlags.ConquerAngle.North, 1);
        Assert.Equal(0u, x); Assert.Equal(0u, y);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositivePushbackDoesNotReadMissingGridOrNormalizePosition(int paces)
    {
        var map = Map();
        map.cells = null!;
        uint x = uint.MaxValue, y = uint.MaxValue;
        map.Pushback(ref x, ref y, LegacyNativePlayerMapFlags.ConquerAngle.North, paces);
        Assert.Equal(uint.MaxValue, x); Assert.Equal(uint.MaxValue, y);
    }
}
