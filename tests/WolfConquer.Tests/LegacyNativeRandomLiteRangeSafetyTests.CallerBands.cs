namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRandomLiteRangeSafetyTests
{
    [Theory]
    [InlineData(1, 13)]
    [InlineData(10, 200)]
    [InlineData(1000, 3500)]
    [InlineData(500, 2000)]
    [InlineData(500, 2500)]
    [InlineData(50, 250)]
    [InlineData(50, 500)]
    [InlineData(50, 300)]
    [InlineData(10, 100)]
    public void ActualChiTypeAndStatBandsKeepTheirSeededInclusiveMapping(int lowerBound, int upperBound)
        => AssertRangeSequence(lowerBound, upperBound, singleUpper: false);
}
