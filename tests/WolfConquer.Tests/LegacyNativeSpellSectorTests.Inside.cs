using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSpellSectorTests
{
    [Theory]
    [InlineData(11, 10, false)]
    [InlineData(10, 11, true)]
    [InlineData(9, 10, true)]
    [InlineData(10, 9, true)]
    [InlineData(11, 11, true)]
    [InlineData(12, 11, false)]
    [InlineData(11, 12, true)]
    public void InsideUsesActualNativeAngularBoundsRatherThanAnIdealizedCone(int x, int y, bool expected)
    {
        var sector = new LegacyNativeSpellSector(10, 10, 10, 11);
        sector.Arrange(90, 14);
        Assert.Equal(expected, sector.Inside(x, y));
    }

    [Theory]
    [InlineData(24, 10, true)]
    [InlineData(10, 24, true)]
    [InlineData(24, 24, true)]
    [InlineData(25, 10, false)]
    [InlineData(10, 25, false)]
    public void RangeIsInclusiveNativeChebyshevAndNeverAboveFourteen(int x, int y, bool expected)
    {
        var sector = new LegacyNativeSpellSector(10, 10, 11, 10);
        sector.Arrange(90, int.MaxValue);
        Assert.Equal(expected, sector.Inside(x, y));
    }

    [Theory]
    [InlineData(32767, 0, false)]
    [InlineData(32768, 0, true)]
    [InlineData(65535, 0, true)]
    [InlineData(-1, 0, true)]
    [InlineData(0, 65535, true)]
    [InlineData(65536, 0, true)]
    public void QueryCoordinatesNarrowToUshortThenNativeDistanceNarrowsToSignedShort(int x, int y, bool expected)
    {
        var sector = new LegacyNativeSpellSector(0, 0, 1, 0);
        sector.Arrange(90, 0);
        Assert.Equal(expected, sector.Inside(x, y));
    }

    [Fact]
    public void NegativeDistanceIsNotClampedAndCanStillAdmitTheNativeWideCoordinateWrap()
    {
        var sector = new LegacyNativeSpellSector(0, 0, 1, 0);
        sector.Arrange(90, -1);
        Assert.False(sector.Inside(0, 0));
        Assert.True(sector.Inside(65535, 65535));
    }
}
