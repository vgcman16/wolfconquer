using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ChineseLuaCatalogTransformTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(24)]
    public void KeyMustHaveTheExactProvenSize(int length)
    {
        var input = Convert.FromHexString(EncodedBlock);
        var key = new byte[length];

        Assert.Throws<ArgumentException>(() => ChineseLuaCatalogTransform.Decode(input, key, input.Length));
        Assert.Equal(Convert.FromHexString(EncodedBlock), input);
        Assert.Equal(new byte[length], key);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(67108865)]
    [InlineData(int.MaxValue)]
    public void CallerBoundMustBePositiveAndWithinTheOfflineLimit(int maximum)
    {
        var input = Convert.FromHexString(EncodedBlock);

        Assert.Throws<ArgumentOutOfRangeException>(() => ChineseLuaCatalogTransform.Decode(input, new byte[12], maximum));
        Assert.Equal(Convert.FromHexString(EncodedBlock), input);
    }

    [Fact]
    public void EmptyInputDoesNotBecomeASuccessfulEmptyCatalog()
    {
        Assert.Throws<InvalidDataException>(() => ChineseLuaCatalogTransform.Decode([], new byte[12], 12));
    }

    [Theory]
    [InlineData(11, 10)]
    [InlineData(12, 11)]
    [InlineData(13, 12)]
    public void InputExceedingTheCallerBoundIsRefusedBeforeAnyMutation(int length, int maximum)
    {
        var input = Convert.FromHexString(EncodedBlock).Concat(new byte[] { 0xAB }).Take(length).ToArray();
        var before = input.ToArray();

        Assert.Throws<InvalidDataException>(() => ChineseLuaCatalogTransform.Decode(input, new byte[12], maximum));
        Assert.Equal(before, input);
    }

    [Fact]
    public void ExactOfflineMaximumAllowsAnOtherwiseBoundedKnownAnswerWithoutLargeAllocation()
    {
        var input = Convert.FromHexString(EncodedBlock);

        Assert.Equal(Convert.FromHexString(ExpectedBlock), ChineseLuaCatalogTransform.Decode(input, new byte[12], 67108864));
    }
}
