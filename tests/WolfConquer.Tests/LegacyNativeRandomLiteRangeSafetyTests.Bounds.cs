using System.Numerics;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRandomLiteRangeSafetyTests
{
    [Theory]
    [InlineData(int.MinValue, int.MinValue)]
    [InlineData(int.MinValue, int.MinValue + 1)]
    [InlineData(int.MinValue, -2)]
    [InlineData(int.MinValue, -1)]
    [InlineData(int.MinValue, 0)]
    [InlineData(int.MinValue, int.MaxValue - 1)]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(int.MinValue + 1, int.MaxValue)]
    [InlineData(int.MinValue + 1, int.MaxValue - 1)]
    [InlineData(-1, int.MaxValue)]
    [InlineData(0, int.MaxValue)]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(-100, 100)]
    [InlineData(-1, -1)]
    public void OrderedSignedIntervalsHavePositiveWideWidthAndStayWithinBothBounds(int lowerBound, int upperBound)
        => AssertRangeSequence(lowerBound, upperBound, singleUpper: false);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(13)]
    [InlineData(int.MaxValue - 1)]
    [InlineData(int.MaxValue)]
    public void NonnegativeUpperBoundsStayInclusiveWithoutOverflowOrExtraDraws(int upperBound)
        => AssertRangeSequence(0, upperBound, singleUpper: true);

    private static void AssertRangeSequence(int lowerBound, int upperBound, bool singleUpper)
    {
        int[] seeds = [int.MinValue, -1, 0, 1, 97, int.MaxValue];
        BigInteger width = (new BigInteger(upperBound) - lowerBound) + BigInteger.One;
        Assert.InRange(width, BigInteger.One, new BigInteger(uint.MaxValue) + BigInteger.One);
        foreach (int seed in seeds)
        {
            var actual = new LegacyNativeRandomLite(seed);
            var unbounded = new LegacyNativeRandomLite(seed);
            for (int draw = 0; draw < 64; draw++)
            {
                // Independent arbitrary-precision range arithmetic; the unchanged
                // genuine unbounded owner supplies the draw, not a fake provider.
                BigInteger expected = lowerBound == upperBound
                    ? lowerBound : lowerBound + (new BigInteger(unbounded.Next()) % width);
                int value = singleUpper ? actual.Next(upperBound) : actual.Next(lowerBound, upperBound);
                Assert.InRange(value, lowerBound, upperBound);
                Assert.Equal((int)expected, value);
            }
            // Includes constant ranges: they must not consume even the first draw.
            Assert.Equal(unbounded.Next(), actual.Next());
        }
    }
}
