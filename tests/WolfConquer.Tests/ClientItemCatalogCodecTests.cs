using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientItemCatalogCodecTests
{
    // All full-block fixtures are authored synthetic data. These algebraic tests
    // are not captured-client byte or runtime compatibility evidence.
    [Fact]
    public void SyntheticBlocksAreIndependentLengthPreservingAndDoNotMutateInputs()
    {
        var plain = Enumerable.Range(0, 36).Select(index => (byte)(index * 7)).ToArray();
        var preserved = plain.ToArray();
        var packed = ClientItemCatalogCodec.Encode(plain);
        var preservedPacked = packed.ToArray();
        var decoded = ClientItemCatalogCodec.Decode(packed);

        Assert.Equal(12, ClientItemCatalogCodec.BlockSize);
        Assert.Equal(plain.Length, packed.Length);
        Assert.Equal(plain, decoded);
        Assert.NotSame(plain, packed);
        Assert.NotSame(packed, decoded);
        for (var offset = 0; offset < plain.Length; offset += 12)
        {
            Assert.Equal(packed.AsSpan(offset, 12).ToArray(), ClientItemCatalogCodec.Encode(plain.AsSpan(offset, 12)));
            Assert.Equal(plain.AsSpan(offset, 12).ToArray(), ClientItemCatalogCodec.Decode(packed.AsSpan(offset, 12)));
        }
        decoded[0] ^= 0xFF;
        Assert.Equal(preserved, plain);
        Assert.Equal(preservedPacked, packed);
    }

    [Theory]
    [InlineData(0)]
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
    public void EveryTrailingLengthMatchesAuthoredLiteralXorTailBytes(int length)
    {
        byte[] tailPlain = [0x00, 0x01, 0x27, 0x40, 0x54, 0x7F, 0x80, 0xAA, 0xD4, 0xFE, 0xFF];
        byte[] tailPacked = [0x54, 0x55, 0x73, 0x14, 0x00, 0x2B, 0xD4, 0xFE, 0x80, 0xAA, 0xAB];
        var shortPlain = tailPlain[..length];
        var shortPacked = tailPacked[..length];
        var blockPlain = Enumerable.Range(0, 12).Select(index => (byte)(index * 13)).ToArray();
        var blockPacked = ClientItemCatalogCodec.Encode(blockPlain);
        var withTailPlain = blockPlain.Concat(shortPlain).ToArray();
        var withTailPacked = blockPacked.Concat(shortPacked).ToArray();

        Assert.Equal(shortPacked, ClientItemCatalogCodec.Encode(shortPlain));
        Assert.Equal(shortPlain, ClientItemCatalogCodec.Decode(shortPacked));
        Assert.Equal(withTailPacked, ClientItemCatalogCodec.Encode(withTailPlain));
        Assert.Equal(withTailPlain, ClientItemCatalogCodec.Decode(withTailPacked));
        Assert.Equal(length, ClientItemCatalogCodec.Encode(shortPlain).Length);
        Assert.Equal(12 + length, ClientItemCatalogCodec.Decode(withTailPacked).Length);
        Assert.Equal(tailPlain[..length], shortPlain);
        Assert.Equal(tailPacked[..length], shortPacked);
    }

    [Fact]
    public void NonEmptyResultsAreIndependentOwnedArraysAcrossRepeatedCalls()
    {
        var plain = Enumerable.Range(0, 36).Select(index => (byte)(255 - index)).ToArray();
        var packed = ClientItemCatalogCodec.Encode(plain);
        var firstEncoded = ClientItemCatalogCodec.Encode(plain);
        var secondEncoded = ClientItemCatalogCodec.Encode(plain);
        var firstDecoded = ClientItemCatalogCodec.Decode(packed);
        var secondDecoded = ClientItemCatalogCodec.Decode(packed);

        Assert.NotSame(firstEncoded, secondEncoded);
        Assert.NotSame(firstDecoded, secondDecoded);
        firstEncoded[5] ^= 0xFF;
        firstDecoded[7] ^= 0xFF;
        Assert.Equal(packed, secondEncoded);
        Assert.Equal(plain, secondDecoded);
    }

    [Fact]
    public void EmptyInputIsAcceptedWithoutInventingPadding()
    {
        Assert.Empty(ClientItemCatalogCodec.Encode(ReadOnlySpan<byte>.Empty));
        Assert.Empty(ClientItemCatalogCodec.Decode(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void DeterministicRandomLengthsVerifyOnlyAlgebraicRoundtripNotANativeOracle()
    {
        var lengths = Enumerable.Range(0, 40).Concat([255, 256, 257, 1023, 4097]);
        var random = new Random(9527);
        foreach (var length in lengths)
        {
            var input = new byte[length];
            random.NextBytes(input);
            var preserved = input.ToArray();
            var encoded = ClientItemCatalogCodec.Encode(input);
            var decoded = ClientItemCatalogCodec.Decode(input);
            Assert.Equal(length, encoded.Length);
            Assert.Equal(length, decoded.Length);
            Assert.Equal(input, ClientItemCatalogCodec.Decode(encoded));
            Assert.Equal(input, ClientItemCatalogCodec.Encode(decoded));
            Assert.Equal(preserved, input);
        }
    }
}
