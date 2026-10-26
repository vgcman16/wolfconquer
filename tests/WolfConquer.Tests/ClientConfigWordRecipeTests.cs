using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigWordRecipeTests
{
    [Fact]
    public void IndependentSequentialWordGoldenPinsAdjacentXorAndBothRotationWraps()
    {
        var encoded = Enumerable.Range(0, 64).Select(value => (uint)value).ToArray();
        uint[] expected =
        [
            31, 1, 3, 1, 7, 1, 3, 1, 15, 1, 3, 1, 7, 1, 3, 1,
            63, 1, 3, 1, 7, 1, 3, 1, 15, 1, 3, 1, 7, 1, 3, 1,
            31, 1, 3, 1, 7, 1, 3, 1, 15, 1, 3, 1, 7, 1, 3, 1,
            63, 1, 3, 1, 7, 1, 3, 1, 15, 1, 3, 1, 7, 1, 3, 1
        ];

        Assert.Equal(expected, ClientConfigWordRecipe.Decode(encoded));
        Assert.Equal(encoded, ClientConfigWordRecipe.Encode(expected));
    }

    [Fact]
    public void FinalHighBitWordFeedsItsAdjacentDifferenceAndDedicatedFinalSlot()
    {
        var encoded = new uint[64];
        encoded[63] = 0x80000000;
        var expected = new uint[64];
        expected[15] = expected[16] = 0x80000000;

        Assert.Equal(expected, ClientConfigWordRecipe.Decode(encoded));
        Assert.Equal(encoded, ClientConfigWordRecipe.Encode(expected));
    }

    [Fact]
    public void ArbitraryUnsignedWordRoundtripOwnsBothOutputsWithoutMutatingInput()
    {
        var original = Enumerable.Range(0, 64).Select(value => unchecked(0xFEDCBA98u + (uint)value * 0x01020305u)).ToArray();
        var preserved = original.ToArray();

        var decoded = ClientConfigWordRecipe.Decode(original);
        var roundtrip = ClientConfigWordRecipe.Encode(decoded);

        Assert.Equal(preserved, original);
        Assert.Equal(preserved, roundtrip);
        Assert.NotSame(original, decoded);
        Assert.NotSame(original, roundtrip);
        Array.Fill(decoded, 0u);
        Assert.Equal(preserved, roundtrip);
        Array.Fill(roundtrip, 0u);
        Assert.Equal(preserved, original);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(65)]
    public void IncompleteOrExtraWordRecipesRefuseWithoutChangingInput(int count)
    {
        var input = Enumerable.Repeat(0x12345678u, count).ToArray();
        var preserved = input.ToArray();
        Assert.Throws<ArgumentException>(() => ClientConfigWordRecipe.Decode(input));
        Assert.Throws<ArgumentException>(() => ClientConfigWordRecipe.Encode(input));
        Assert.Equal(preserved, input);
    }
}
