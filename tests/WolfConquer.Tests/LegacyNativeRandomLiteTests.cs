using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class LegacyNativeRandomLiteTests
{
    [Fact]
    public void SeededSequenceRetainsNativeXorShiftConstantsAndUncheckedUIntState()
    {
        var random = new LegacyNativeRandomLite(1);
        int[] expected = [273329069,512579540,935370717,1925318465,932236663,1129783206,1893359206,314619699,587873129,1482406529,2052122242,1832824788];
        Assert.Equal(expected, Enumerable.Range(0, expected.Length).Select(_ => random.Next()));
        random.Reseed(-1);
        Assert.Equal(new[] {273327196,512579621,935366700,1925318832,938425072,1129779288,1889905134,314620098}, Enumerable.Range(0,8).Select(_ => random.Next()));
        random.Reseed(1); Assert.Equal(273329069, random.Next());
    }

    [Fact]
    public void ZeroUpperBoundAndEqualRangeReturnWithoutConsumingStateButPositiveRangeIncludesUpper()
    {
        var random = new LegacyNativeRandomLite(97);
        Assert.Equal(0, random.Next(0)); Assert.Equal(-7, random.Next(-7,-7));
        Assert.Equal(273262797, random.Next());
        random.Reseed(97);
        Assert.Equal(1, random.Next(1)); // First odd draw proves inclusive upper1, not Random.Next's exclusive1.
        Assert.Equal(2, random.Next(1,13)); // Second draw512644276%13+1 =2.
    }

    [Fact]
    public void InvalidRangesKeepNativeParameterDetailsAndDoNotDraw()
    {
        var random = new LegacyNativeRandomLite(97);
        var one = Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
        Assert.Equal("upperBound", one.ParamName); Assert.Equal(-1, one.ActualValue);
        var two = Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(5,4));
        Assert.Equal("upperBound", two.ParamName); Assert.Equal(4, two.ActualValue);
        Assert.Equal(273262797, random.Next());
    }

    [Fact]
    public void FullSignedRangeHasNoArithmeticFaultAndConsumesOneRealDraw()
    {
        var random = new LegacyNativeRandomLite(97);
        int value = random.Next(int.MinValue, int.MaxValue);
        Assert.Equal(-1874220851, value);
        Assert.InRange(value, int.MinValue, int.MaxValue);
        Assert.Equal(512644276, random.Next());
        random.Reseed(97);
        Assert.Equal(273262797, random.Next(int.MaxValue));
        Assert.Equal(512644276, random.Next());
    }
}
