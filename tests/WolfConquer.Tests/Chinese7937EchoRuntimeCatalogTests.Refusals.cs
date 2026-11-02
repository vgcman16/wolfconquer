using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoRuntimeCatalogTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(13)]
    public void NoDefaultTransformKeyIsSelected(int length) => Refuses(Capture(),
        Chinese7937EchoRuntimeFixture.CompleteEncoded(), new byte[length],
        SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded()), Chinese7937EchoRuntimeCatalogFailure.KeyRequired);

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void DecodedIdentityMustBeExplicitAndFullWidth(int length) => Refuses(Capture(),
        Chinese7937EchoRuntimeFixture.CompleteEncoded(), new byte[12], new byte[length],
        Chinese7937EchoRuntimeCatalogFailure.PinRequired);

    [Fact]
    public void ForeignEncodedImageWrongKeyAndWrongDecodedPinCannotBorrowCapturedRelationship()
    {
        var encoded = Chinese7937EchoRuntimeFixture.CompleteEncoded();
        var captured = Capture(encoded);
        var changed = encoded.ToArray(); changed[0] ^= 1;
        var decodedPin = SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded());
        Refuses(captured, changed, new byte[12], decodedPin, Chinese7937EchoRuntimeCatalogFailure.PinMismatch);
        var key = new byte[12]; key[0] = 1;
        Refuses(captured, encoded, key, decodedPin, Chinese7937EchoRuntimeCatalogFailure.PinMismatch);
        Assert.Equal((byte)1, key[0]);
        decodedPin[0] ^= 1;
        Refuses(captured, encoded, new byte[12], decodedPin, Chinese7937EchoRuntimeCatalogFailure.PinMismatch);
    }

    [Fact]
    public void Historical65EnvelopesAndOtherClientVersionsStaySeparate()
    {
        var encoded = Chinese7937EchoRuntimeFixture.CompleteEncoded();
        var pin = SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded());
        Refuses(Capture(fields: 65), encoded, new byte[12], pin, Chinese7937EchoRuntimeCatalogFailure.ItemEnvelopeRequired);
        Refuses(Capture(fields: 65, version: 7931), encoded, new byte[12], pin,
            Chinese7937EchoRuntimeCatalogFailure.UnsupportedVersion);
        Refuses(null!, encoded, new byte[12], pin, Chinese7937EchoRuntimeCatalogFailure.SourceRequired);
    }

    [Fact]
    public void EmptyOversizedAndIncompleteInfoNeverPublish()
    {
        var captured = Capture();
        var pin = SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded());
        Refuses(captured, [], new byte[12], pin, Chinese7937EchoRuntimeCatalogFailure.InputBounds);
        Refuses(captured, new byte[Chinese7937DecodedXuanbaoInfo.MaximumBytes + 1], new byte[12], pin,
            Chinese7937EchoRuntimeCatalogFailure.InputBounds);
        var incomplete = Chinese7937EchoRuntimeFixture.IncompleteEncoded();
        Refuses(Capture(incomplete), incomplete, new byte[12],
            SHA256.HashData(Chinese7937EchoRuntimeFixture.IncompleteDecoded()),
            Chinese7937EchoRuntimeCatalogFailure.UnsupportedRequirements);
    }

    [Fact]
    public void CorrectlyPinnedOtherLiteralSchemaDoesNotBecomeInfo()
    {
        var encoded = ChineseEncodedEchoIntakeTests.Encoded();
        Refuses(Capture(encoded), encoded, new byte[12], SHA256.HashData(ChineseEncodedEchoIntakeTests.Decoded()),
            Chinese7937EchoRuntimeCatalogFailure.InvalidInfo);
    }

    [Fact]
    public void FailedLaterPreparationLeavesEarlierRealCatalogAndRequestIntact()
    {
        var catalog = Create();
        Assert.True(catalog.TryPrepareActivationRequest(0, out var request));
        var expected = request!.ToFrameBytes();
        Refuses(Capture(main: [42, 43]), Chinese7937EchoRuntimeFixture.CompleteEncoded(), new byte[12],
            SHA256.HashData(Chinese7937EchoRuntimeFixture.CompleteDecoded()), Chinese7937EchoRuntimeCatalogFailure.MissingItemReference);
        Assert.Equal(expected, request.ToFrameBytes());
        Assert.True(catalog.TryFindItem(44, out var retained));
        Assert.NotNull(retained.Main);
    }

    private static void Refuses(ChineseFullClientCatalogSnapshot captured, byte[] encoded, byte[] key, byte[] pin,
        Chinese7937EchoRuntimeCatalogFailure expected)
    {
        Assert.False(Chinese7937EchoRuntimeCatalog.TryCreate(captured, encoded, key, pin, out var catalog, out var failure));
        Assert.Null(catalog);
        Assert.Equal(expected, failure);
    }
}
