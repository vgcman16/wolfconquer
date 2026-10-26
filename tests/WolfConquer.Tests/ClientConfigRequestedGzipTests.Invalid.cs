using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigRequestedGzipTests
{
    [Theory]
    [InlineData("block-type")]
    [InlineData("stored-complement")]
    [InlineData("literal-286")]
    [InlineData("literal-287")]
    [InlineData("distance-30")]
    [InlineData("distance-31")]
    [InlineData("missing-history")]
    public void ActualInflateRejectsInvalidBlockSymbolsAndHistory(string shape)
    {
        byte[] raw;
        if (shape == "block-type") raw = [7];
        else if (shape == "stored-complement") raw = [1, 1, 0, 0, 0, 65];
        else
        {
            var bits = new ClientConfigDeflateTestBits().Low(1, 1).Low(1, 2);
            if (shape.StartsWith("literal", StringComparison.Ordinal))
                bits.Code(shape == "literal-286" ? "11000110" : "11000111");
            else
            {
                if (shape != "missing-history") bits.Code("01110001"); // Literal A.
                bits.Code("0000001"); // Length257: three bytes, no extra bits.
                bits.Code(shape == "distance-30" ? "11110" : shape == "distance-31" ? "11111" : "00000");
            }
            raw = bits.ToArray();
        }
        var bytes = Wrap(raw, 0, 1);
        var before = bytes.ToArray();
        Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(bytes, 1));
        Assert.Equal(before, bytes);
    }

    [Theory]
    [InlineData("too-short")]
    [InlineData("magic")]
    [InlineData("truncated-header")]
    [InlineData("caller-bound")]
    [InlineData("signed-size")]
    [InlineData("offline-size")]
    public void InvalidOuterCandidatesBoundsAndTruncatedHeadersRefuse(string shape)
    {
        var bytes = shape switch
        {
            "too-short" => new byte[] { 0x1f, 0x8b, 0, 0 },
            "magic" => new byte[] { 0, 0, 1, 0, 0, 0 },
            "truncated-header" => new byte[] { 0x1f, 0x8b, 8, 1, 0, 0, 0 },
            "signed-size" => Request(ClientConfigRequestedGzipFixtures.SmallFixed(), 0x80000000),
            "offline-size" => Request(ClientConfigRequestedGzipFixtures.SmallFixed(), 64 * 1024 * 1024 + 1),
            _ => ClientConfigRequestedGzipFixtures.SmallFixed()
        };
        var maximum = shape == "caller-bound" ? 2 : int.MaxValue;
        Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(bytes, maximum));
    }

    [Fact]
    public void NegativeCallerBoundRefusesClearly() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientConfigRequestedGzip.Decode(ClientConfigRequestedGzipFixtures.SmallFixed(), -1));
}
