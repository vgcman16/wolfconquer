using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigDefaultContentsTests
{
    // Owned synthetic arithmetic goldens shared with the explicit-profile proof;
    // no native modulus, filename, configuration or credential is bundled.
    private const string ModulusHex = "FFFFFFFFFFFFFF720000000000001321";
    private const string BinaryBlockHex = "F422BEBCB2583EF983DF19472A45CC44";
    private const string ShortBlockHex = "1A541A1B9B65374B641C3A6DF60C87";
    private static readonly byte[] BinaryPayload = [0x10, 0x20, 0, 0x80, 0xFF];

    [Fact]
    public void FixedWidthBlocksAccumulateOrderedOwnedBinaryWithoutEncodingAssumptions()
    {
        var encoded = Convert.FromHexString(BinaryBlockHex + BinaryBlockHex);
        var modulus = Convert.FromHexString(ModulusHex);
        var preservedEncoded = encoded.ToArray();
        var preservedModulus = modulus.ToArray();

        var result = ClientConfigDefaultContents.DecodeChunks(encoded, modulus, 10);

        Assert.Equal(BinaryPayload.Concat(BinaryPayload), result);
        Assert.Equal(preservedEncoded, encoded);
        Assert.Equal(preservedModulus, modulus);
        result[0] ^= 0xFF;
        Assert.Equal(preservedEncoded, encoded);
        Assert.Equal(BinaryPayload.Concat(BinaryPayload), ClientConfigDefaultContents.DecodeChunks(encoded, modulus, int.MaxValue));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LastReadMayBeShortAndNumericalWidthIgnoresModulusRepresentationZeroes(bool paddedModulus)
    {
        var encoded = Convert.FromHexString(BinaryBlockHex + ShortBlockHex);
        var modulus = Convert.FromHexString((paddedModulus ? "00000000" : "") + ModulusHex);

        var result = ClientConfigDefaultContents.DecodeChunks(encoded, modulus, 6);

        Assert.Equal(new byte[] { 0x10, 0x20, 0, 0x80, 0xFF, 65 }, result);
        Assert.Equal(Convert.FromHexString(BinaryBlockHex + ShortBlockHex), encoded);
    }

    [Fact]
    public void AShortSingleReadIsNotLeftPaddedOrConfusedWithAFullChunkBoundary()
    {
        var encoded = Convert.FromHexString(ShortBlockHex);
        Assert.Equal(new byte[] { 65 }, ClientConfigDefaultContents.DecodeChunks(encoded, Convert.FromHexString(ModulusHex), 1));
    }

    [Fact]
    public void LateOutputLimitFailureDoesNotReturnAnEarlierDecodedPrefix()
    {
        var encoded = Convert.FromHexString(BinaryBlockHex + ShortBlockHex);
        var preserved = encoded.ToArray();
        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultContents.DecodeChunks(encoded, Convert.FromHexString(ModulusHex), 5));
        Assert.Equal(preserved, encoded);
        Assert.Equal(new byte[] { 0x10, 0x20, 0, 0x80, 0xFF, 65 }, ClientConfigDefaultContents.DecodeChunks(encoded, Convert.FromHexString(ModulusHex), 6));
    }

    [Theory]
    [InlineData("zero-payload-first")]
    [InlineData("zero-payload-later")]
    [InlineData("invalid-later")]
    [InlineData("truncated-final")]
    public void InvalidOrNonpositiveChunkOutputRefusesTheWholeContents(string kind)
    {
        var hex = kind switch
        {
            "zero-payload-first" => "F6D24BC40C24D167EA5B5DBC1A66121F" + BinaryBlockHex,
            "zero-payload-later" => BinaryBlockHex + "F6D24BC40C24D167EA5B5DBC1A66121F",
            "invalid-later" => BinaryBlockHex + new string('0', 32),
            _ => BinaryBlockHex + "00"
        };
        var encoded = Convert.FromHexString(hex);
        var preserved = encoded.ToArray();
        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultContents.DecodeChunks(encoded, Convert.FromHexString(ModulusHex), int.MaxValue));
        Assert.Equal(preserved, encoded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveCallerOutputLimitsRefuseBeforeDecoding(int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientConfigDefaultContents.DecodeChunks(Array.Empty<byte>(), Array.Empty<byte>(), maximum));
    }

    [Fact]
    public void EmptyInputIsNotAnEndOfFileSuccess()
    {
        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultContents.DecodeChunks(Array.Empty<byte>(), Convert.FromHexString(ModulusHex), 1));
    }

    [Fact]
    public void InvalidModulusCannotBecomeAnEmptyOrIdentityTransform()
    {
        var encoded = Convert.FromHexString(BinaryBlockHex);
        Assert.Throws<ArgumentException>(() => ClientConfigDefaultContents.DecodeChunks(encoded, new byte[16], 5));
        Assert.Equal(Convert.FromHexString(BinaryBlockHex), encoded);
    }
}
