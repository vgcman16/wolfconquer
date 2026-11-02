using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionRawCatalogTests
{
    [Theory]
    [InlineData("1@@2@@3@@4@@5@@")]
    [InlineData("1@@2@@3@@4@@5@@\n")]
    [InlineData("1@@2@@3@@4@@5@@\r")]
    [InlineData("\r\n1@@2@@3@@4@@5@@\r\n")]
    [InlineData("1@@2@@3@@4@@5@@\r\n\r\n")]
    [InlineData("1@@2@@3@@4@@\r\n")]
    [InlineData("1@@2@@3@@4@@5@@6@@\r\n")]
    [InlineData("1@@2@@3@@4@@5@@tail\r\n")]
    [InlineData("1@@+2@@3@@4@@5@@\r\n")]
    [InlineData("1@@-2@@3@@4@@5@@\r\n")]
    [InlineData("1@@ 2@@3@@4@@5@@\r\n")]
    [InlineData("1@@2.0@@3@@4@@5@@\r\n")]
    [InlineData("1@@0x02@@3@@4@@5@@\r\n")]
    [InlineData("1@@2@@3@@4@@5@@\r\n2@@2@@3@@4@@bad@@\r\n")]
    [InlineData("Id@@A@@B@@C@@D@@\r\n")]
    [InlineData("1@@2@@3@@4@@\0@@\r\n")]
    public void CompleteMalformedCapturesIncludingTailsReturnNoPartialCatalog(string raw) => Refuses(
        Encoding.ASCII.GetBytes(raw), Chinese7937XuanbaoAdditionRawFailure.UnsupportedSyntax);

    [Theory]
    [InlineData("4294967296@@1@@2@@3@@4@@\r\n")]
    [InlineData("1@@2@@3@@4@@4294967296@@\r\n")]
    public void OfflineUnsignedCaptureOverflowRefusesWithoutGuessingNativeWidths(string raw) => Refuses(
        Encoding.ASCII.GetBytes(raw), Chinese7937XuanbaoAdditionRawFailure.NumericWidth);

    [Fact]
    public void NumericallyDuplicateIdentitiesRefuseDespiteDifferentRawSpellings() => Refuses(
        Encoding.ASCII.GetBytes("1@@2@@3@@4@@5@@\r\n001@@2@@3@@4@@5@@\r\n"),
        Chinese7937XuanbaoAdditionRawFailure.DuplicateRowId);

    [Theory]
    [InlineData(0xff)]
    [InlineData(0x80)]
    public void NonAsciiBytesHaveNoTextEncodingFallback(int value)
    {
        var bytes = Encoding.ASCII.GetBytes("1@@2@@3@@4@@5@@\r\n"); bytes[3] = (byte)value;
        Refuses(bytes, Chinese7937XuanbaoAdditionRawFailure.UnsupportedSyntax);
    }

    [Fact]
    public void ExplicitProfilePinAndAllocationBoundsPrecedeDecode()
    {
        var packed = PackedGolden(); var pin = SHA256.HashData(packed);
        Assert.False(Chinese7937XuanbaoAdditionRawCatalog.TryParsePacked(7931, packed, pin, out var result, out var failure));
        Assert.Null(result); Assert.Equal(Chinese7937XuanbaoAdditionRawFailure.UnsupportedVersion, failure);
        Assert.False(Chinese7937XuanbaoAdditionRawCatalog.TryParsePacked(7937, packed, [], out result, out failure));
        Assert.Null(result); Assert.Equal(Chinese7937XuanbaoAdditionRawFailure.PinRequired, failure);
        pin[0] ^= 1;
        Assert.False(Chinese7937XuanbaoAdditionRawCatalog.TryParsePacked(7937, packed, pin, out result, out failure));
        Assert.Null(result); Assert.Equal(Chinese7937XuanbaoAdditionRawFailure.PinMismatch, failure);
        Assert.False(Chinese7937XuanbaoAdditionRawCatalog.TryParsePacked(7937, [], new byte[32], out result, out failure));
        Assert.Null(result); Assert.Equal(Chinese7937XuanbaoAdditionRawFailure.InputBounds, failure);
        Assert.False(Chinese7937XuanbaoAdditionRawCatalog.TryParsePacked(7937,
            new byte[Chinese7937XuanbaoAdditionRawCatalog.MaximumBytes + 1], new byte[32], out result, out failure));
        Assert.Null(result); Assert.Equal(Chinese7937XuanbaoAdditionRawFailure.InputBounds, failure);
    }

    [Fact]
    public void CompleteRowBoundHasNoTruncatingSuccess()
    {
        var text = string.Concat(Enumerable.Range(1, Chinese7937XuanbaoAdditionRawCatalog.MaximumRows + 1)
            .Select(id => FormattableString.Invariant($"{id}@@2@@3@@4@@5@@\r\n")));
        Refuses(Encoding.ASCII.GetBytes(text), Chinese7937XuanbaoAdditionRawFailure.RowBounds);
    }

    private static void Refuses(byte[] decoded, Chinese7937XuanbaoAdditionRawFailure expected)
    {
        var packed = ClientItemCatalogCodec.Encode(decoded);
        Assert.False(Chinese7937XuanbaoAdditionRawCatalog.TryParsePacked(7937, packed, SHA256.HashData(packed), out var result, out var failure));
        Assert.Null(result); Assert.Equal(expected, failure);
    }
}
