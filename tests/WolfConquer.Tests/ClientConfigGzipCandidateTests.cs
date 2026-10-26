using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigGzipCandidateTests
{
    [Theory]
    [InlineData("1F8B9978563412", 0x12345678u)]
    [InlineData("1F8B99FFFFFFFF", uint.MaxValue)]
    [InlineData("1F8B9900000000", 0u)]
    public void CandidateTrailerIsRawLittleEndianMetadataNotStreamValidation(string hex, uint expected)
    {
        // Deliberately not complete gzip streams: this API reports only the
        // observed candidate fields and never inflates or authorizes allocation.
        var bytes = Convert.FromHexString(hex);
        var original = bytes.ToArray();
        Assert.Equal(expected, ClientConfigGzipCandidate.ReadDeclaredOutputLength(bytes));
        Assert.Equal(original, bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1F")]
    [InlineData("1F8B")]
    [InlineData("1F8B0000")]
    [InlineData("1F8C0000000000")]
    [InlineData("001F8B00000000")]
    public void ShortOrDifferentPrefixIsNotARecognizedGzipCandidate(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        var original = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => ClientConfigGzipCandidate.ReadDeclaredOutputLength(bytes));
        Assert.Equal(original, bytes);
    }
}
