using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigDefaultPublicTransformTests
{
    // Owned synthetic 128-bit arithmetic fixture. These are not client words,
    // a native modulus, configuration data or a credential.
    private const string ModulusHex = "FFFFFFFFFFFFFF720000000000001321";
    private const string BinaryBlockHex = "F422BEBCB2583EF983DF19472A45CC44";
    private static readonly byte[] BinaryPayload = [0x10, 0x20, 0, 0x80, 0xFF];

    [Theory]
    [InlineData(5)]
    [InlineData(17)]
    [InlineData(int.MaxValue)]
    public void IndependentSignedGoldenRecoversExactBinaryWithoutUsingCapacityAsAnAllocationSize(int capacity)
    {
        var encoded = Convert.FromHexString(BinaryBlockHex);
        var modulus = Convert.FromHexString(ModulusHex);
        var originalEncoded = encoded.ToArray();
        var originalModulus = modulus.ToArray();

        var result = ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, capacity);

        Assert.Equal(BinaryPayload, result);
        Assert.Equal(originalEncoded, encoded);
        Assert.Equal(originalModulus, modulus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeInputLengthAllowsBothFullAndShortBigEndianRepresentations(bool shortInput)
    {
        var encoded = Convert.FromHexString("001A541A1B9B65374B641C3A6DF60C87");
        if (shortInput) encoded = encoded[1..];
        var preserved = encoded.ToArray();

        var result = ClientConfigDefaultPublicTransform.DecodeBlock(encoded, Convert.FromHexString(ModulusHex), 1);

        Assert.Equal(new byte[] { 65 }, result);
        Assert.Equal(preserved, encoded);
    }

    [Fact]
    public void ZeroPayloadIsValidForTheLeafWhileTheOuterLoaderMustRequirePositiveOutput()
    {
        var encoded = Convert.FromHexString("F6D24BC40C24D167EA5B5DBC1A66121F");
        Assert.Empty(ClientConfigDefaultPublicTransform.DecodeBlock(encoded, Convert.FromHexString(ModulusHex), 0));
    }

    [Fact]
    public void ExactWordRecipeFeedsTheNativeShiftAndAddModulusOrder()
    {
        // Independent sparse golden for the 64-word inverse recipe, not a call
        // to the production encoder to create its own expected input.
        var words = new uint[64];
        Array.Fill(words, 0x13ACu, 0, 44);
        words[44] = 0xFFFFEC53;
        words[45] = words[46] = 0x1321;
        var before = words.ToArray();
        var encoded = Convert.FromHexString(BinaryBlockHex);

        var result = ClientConfigDefaultPublicTransform.DecodeWordBlock(encoded, words, 5);

        Assert.Equal(BinaryPayload, result);
        Assert.Equal(before, words);
        Assert.Equal(Convert.FromHexString(BinaryBlockHex), encoded);
    }

    [Fact]
    public void NumericalModulusWidthIgnoresLeadingZeroRepresentationBytes()
    {
        var modulus = new byte[20];
        Convert.FromHexString(ModulusHex).CopyTo(modulus, 4);
        var before = modulus.ToArray();

        Assert.Equal(BinaryPayload, ClientConfigDefaultPublicTransform.DecodeBlock(
            Convert.FromHexString(BinaryBlockHex), modulus, 5));
        Assert.Equal(before, modulus);
    }

    [Fact]
    public void EverySuccessfulCallReturnsIndependentOwnedPayloadBytes()
    {
        var encoded = Convert.FromHexString(BinaryBlockHex);
        var modulus = Convert.FromHexString(ModulusHex);
        var first = ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, 5);
        var second = ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, 5);
        first[0] ^= 0xFF;

        Assert.NotSame(first, second);
        Assert.Equal(BinaryPayload, second);
        Assert.Equal(BinaryPayload, ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, 5));
        Assert.Equal(Convert.FromHexString(BinaryBlockHex), encoded);
        Assert.Equal(Convert.FromHexString(ModulusHex), modulus);
    }
}
