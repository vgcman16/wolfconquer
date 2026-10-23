using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSpellSectorTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 360)]
    [InlineData(0, 1, 0, 0, 360)]
    [InlineData(0, 0, 0, 1, 270)]
    [InlineData(0, -1, 0, 0, 180)]
    [InlineData(0, 0, 0, -1, 90)]
    [InlineData(0, 1, 0, 1, 315)]
    [InlineData(0, -1, 0, 1, 225)]
    [InlineData(0, -1, 0, -1, 135)]
    [InlineData(0, 1, 0, -1, 45)]
    [InlineData(int.MinValue, int.MaxValue, 0, 0, 180)]
    [InlineData(int.MaxValue, int.MinValue, 0, 0, 360)]
    [InlineData(0, 0, int.MinValue, int.MaxValue, 90)]
    [InlineData(0, 0, int.MaxValue, int.MinValue, 270)]
    [InlineData(10, 13, 20, 24, 306)]
    public void WholeGetDegreePreservesArgumentOrderAxesAndPreWideningSignedWrap(int x, int x2, int y, int y2, int expected)
    {
        Assert.Equal(expected, LegacyNativeRoleCore.GetDegree(x, x2, y, y2));
    }
}
