using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeRoleDistanceTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(410, 354, 428, 355, 18)]
    [InlineData(428, 355, 410, 354, 18)]
    [InlineData(410, 354, 409, 373, 19)]
    [InlineData(409, 373, 410, 354, 19)]
    [InlineData(0, 0, 32767, 1, 32767)]
    [InlineData(0, 0, 32768, 1, 1)]
    [InlineData(0, 0, 32768, 32768, -32768)]
    [InlineData(0, 0, 65535, 65535, -1)]
    [InlineData(65535, 65535, 0, 0, -1)]
    [InlineData(0, 0, 65535, 0, 0)]
    [InlineData(0, 65535, 1, 0, 1)]
    public void NativeDistanceNarrowsEachAxisBeforeTakingMaximum(
        ushort x, ushort y, ushort otherX, ushort otherY, short expected)
    {
        Assert.Equal(expected, LegacyNativeRoleCore.GetDistance(x, y, otherX, otherY));
    }
}
