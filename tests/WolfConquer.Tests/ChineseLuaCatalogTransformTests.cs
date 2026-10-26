using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ChineseLuaCatalogTransformTests
{
    private const string EncodedBlock = "6EC75940C49DAE83F7EC21AD";
    private const string ExpectedBlock = "010000000100000001000000";
    private static readonly byte[] TailInput = [0, 1, 0x54, 0xAB, 0xFF, 0x80, 0x7F, 0x55, 0xAA, 0xFE, 0x10];
    private static readonly byte[] TailExpected = [0x54, 0x55, 0, 0xFF, 0xAB, 0xD4, 0x2B, 1, 0xFE, 0xAA, 0x44];

    // Published key/plaintext/ciphertext triples from Crypto++ 8.9 TestData/3wayval.dat.
    // Its Dec uses LE words. Native BE words require reversing EACH four-byte
    // word in the reference ciphertext and expected plaintext, leaving the key unchanged.
    [Theory]
    [InlineData("000000000000000000000000", "000000010000000100000001", "4059C76E83AE9DC4AD21ECF7",
        "6EC75940C49DAE83F7EC21AD", "010000000100000001000000")]
    [InlineData("000000060000000500000004", "000000030000000200000001", "D2F05B5ED6144138CAB920CD",
        "5E5BF0D2384114D6CD20B9CA", "030000000200000001000000")]
    [InlineData("DEF01234456789ABBCDEF012", "234567899ABCDEF001234567", "0AA55DBB9CDDDB6D7CDB76B2",
        "BB5DA50A6DDBDD9CB276DB7C", "89674523F0DEBC9A67452301")]
    [InlineData("D2F05B5ED6144138CAB920CD", "4059C76E83AE9DC4AD21ECF7", "478EA8716B13F17C15B155ED",
        "71A88E477CF1136BED55B115", "6EC75940C49DAE83F7EC21AD")]
    public void PublishedKnownAnswersUseTheExplicitNativeWordOrder(string keyHex, string referencePlainHex,
        string referenceCipherHex, string nativeCipherHex, string nativePlainHex)
    {
        var key = Convert.FromHexString(keyHex);
        var input = Convert.FromHexString(nativeCipherHex);
        var expected = Convert.FromHexString(nativePlainHex);
        Assert.Equal(ReverseWords(Convert.FromHexString(referenceCipherHex)), input);
        Assert.Equal(ReverseWords(Convert.FromHexString(referencePlainHex)), expected);

        Assert.Equal(expected, ChineseLuaCatalogTransform.Decode(input, key, input.Length));
        Assert.Equal(Convert.FromHexString(nativeCipherHex), input);
        Assert.Equal(Convert.FromHexString(keyHex), key);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void EveryShortTailLengthRetainsItsExactSizeAndBinaryBytes(int length)
    {
        var input = TailInput.Take(length).ToArray();
        var before = input.ToArray();

        var actual = ChineseLuaCatalogTransform.Decode(input, new byte[12], length);

        Assert.Equal(TailExpected.Take(length).ToArray(), actual);
        Assert.Equal(length, actual.Length);
        Assert.Equal(before, input);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void EveryRemainderStartsAfterTheCompleteKnownAnswerBlockWithoutPadding(int length)
    {
        var input = Convert.FromHexString(EncodedBlock).Concat(TailInput.Take(length)).ToArray();
        var expected = Convert.FromHexString(ExpectedBlock).Concat(TailExpected.Take(length)).ToArray();

        var actual = ChineseLuaCatalogTransform.Decode(input, new byte[12], input.Length);

        Assert.Equal(expected, actual);
        Assert.Equal(12 + length, actual.Length);
    }

    [Fact]
    public void RepeatedFullBlocksAreIndependentAndTheirTailIsProcessedOnce()
    {
        var input = Convert.FromHexString(EncodedBlock + EncodedBlock + EncodedBlock)
            .Concat(TailInput.Take(5)).ToArray();
        var expected = Convert.FromHexString(ExpectedBlock + ExpectedBlock + ExpectedBlock)
            .Concat(TailExpected.Take(5)).ToArray();

        Assert.Equal(expected, ChineseLuaCatalogTransform.Decode(input, new byte[12], input.Length));
    }

    [Fact]
    public void CallerInputKeyAndEachReturnedArrayRemainIndependentlyOwned()
    {
        var input = Convert.FromHexString(EncodedBlock);
        var key = new byte[12];
        var decoded = ChineseLuaCatalogTransform.Decode(input, key, input.Length);
        var repeated = ChineseLuaCatalogTransform.Decode(input, key, input.Length);
        decoded[0] ^= 1;
        Assert.Equal(Convert.FromHexString(ExpectedBlock), repeated);
        Assert.Equal(Convert.FromHexString(EncodedBlock), input);
        Assert.Equal(new byte[12], key);
        Array.Fill(input, (byte)0xCC);
        Array.Fill(key, (byte)0xEE);
        Assert.Equal(Convert.FromHexString(ExpectedBlock), repeated);
    }

    [Fact]
    public void ExplicitInputAndKeySlicesExcludeTheirSurroundingBytes()
    {
        var input = new byte[] { 0xEE }.Concat(Convert.FromHexString(EncodedBlock)).Concat(new byte[] { 0xDD }).ToArray();
        var key = new byte[14];
        key[0] = 0xAA;
        key[^1] = 0xBB;
        var beforeInput = input.ToArray();
        var beforeKey = key.ToArray();

        Assert.Equal(Convert.FromHexString(ExpectedBlock),
            ChineseLuaCatalogTransform.Decode(input.AsSpan(1, 12), key.AsSpan(1, 12), 12));
        Assert.Equal(beforeInput, input);
        Assert.Equal(beforeKey, key);
    }

    private static byte[] ReverseWords(byte[] source)
    {
        Assert.Equal(12, source.Length);
        var result = new byte[source.Length];
        for (var index = 0; index < source.Length; index++) result[index] = source[index ^ 3];
        return result;
    }
}
