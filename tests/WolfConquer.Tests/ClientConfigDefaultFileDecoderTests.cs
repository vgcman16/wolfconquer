using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigDefaultFileDecoderTests
{
    [Fact]
    public void RealDefaultTransformChunksComposeIntoAStandardGzipMember()
    {
        var encoded = ClientConfigRequestedGzipFixtures.EncodedSmallFixed();
        var modulus = ClientConfigRequestedGzipFixtures.Modulus();
        var encodedBefore = encoded.ToArray();
        var modulusBefore = modulus.ToArray();
        var decoded = ClientConfigDefaultFileDecoder.Decode(encoded, modulus, 23, 3);
        Assert.Equal(new byte[] { 97, 98, 99 }, decoded);
        Array.Fill(decoded, (byte)0);
        Assert.Equal(encodedBefore, encoded);
        Assert.Equal(modulusBefore, modulus);
    }

    [Fact]
    public void FourByteMagicPrefixRemainsRawRatherThanInventingAGzipCandidate()
    {
        Assert.Equal(new byte[] { 0x1f, 0x8b, 1, 2 }, ClientConfigDefaultFileDecoder.Decode(
            ClientConfigRequestedGzipFixtures.EncodedFourBytes(), ClientConfigRequestedGzipFixtures.Modulus(), 4, 4));
    }

    [Theory]
    [InlineData(22, 3)]
    [InlineData(23, 2)]
    public void TransformedAndExpandedOutputBoundsAreIndependent(int transformedMaximum, int resultMaximum)
    {
        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultFileDecoder.Decode(
            ClientConfigRequestedGzipFixtures.EncodedSmallFixed(), ClientConfigRequestedGzipFixtures.Modulus(),
            transformedMaximum, resultMaximum));
    }

    [Fact]
    public void NegativeResultBoundRefusesBeforeTryingTheEmptyTransform() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientConfigDefaultFileDecoder.Decode([], [], 0, -1));
}
