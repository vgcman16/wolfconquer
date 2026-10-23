using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

public sealed class LegacyNativeRandomLiteParameterNameTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-7)]
    [InlineData(int.MinValue)]
    [InlineData(int.MinValue + 1)]
    public void NegativeUpperBoundKeepsExactExceptionDetailsAndDoesNotConsumeState(int upperBound)
    {
        var random = new LegacyNativeRandomLite(97);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(upperBound));
        Assert.Equal("upperBound", error.ParamName);
        Assert.Equal(upperBound, error.ActualValue);
        Assert.Equal(new ArgumentOutOfRangeException(nameof(upperBound), upperBound,
            "upperBound must be >=0").Message, error.Message);
        Assert.Equal(273262797, random.Next());
        Assert.Equal(512644276, random.Next());
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(-2, -3)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void ReversedRangeKeepsUpperParameterDetailsAndDoesNotConsumeState(int lowerBound, int upperBound)
    {
        var random = new LegacyNativeRandomLite(97);
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(lowerBound, upperBound));
        Assert.Equal("upperBound", error.ParamName);
        Assert.Equal(upperBound, error.ActualValue);
        Assert.Equal(new ArgumentOutOfRangeException(nameof(upperBound), upperBound,
            "upperBound must be >=lowerBound").Message, error.Message);
        Assert.Equal(273262797, random.Next());
        Assert.Equal(512644276, random.Next());
    }

    [Fact]
    public void RefusalsBetweenRealDrawsLeaveTheSeededSequenceAndReseedingUnchanged()
    {
        var random = new LegacyNativeRandomLite(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
        Assert.Equal(273329069, random.Next());
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(5, 4));
        Assert.Equal(512579540, random.Next());
        random.Reseed(1);
        Assert.Equal(273329069, random.Next());
        Assert.Equal(512579540, random.Next());
    }
}
