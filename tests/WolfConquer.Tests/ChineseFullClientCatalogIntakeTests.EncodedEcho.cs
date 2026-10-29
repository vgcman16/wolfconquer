using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ChineseFullClientCatalogIntakeTests
{
    private static ChineseFullClientCatalogSnapshot CaptureForEncodedEcho(byte[] encoded, int version = 7937) =>
        ChineseFullClientCatalogIntake.ParseCaptured(version, System.Text.Encoding.ASCII.GetBytes(version + "\r\n"),
            ClientItemCatalogCodec.Encode(ItemRows("42")), ClientItemCatalogCodec.Encode(ItemRows("7")), encoded, [0xfe, 2, 3]);

    [Fact]
    public void EncodedAttachmentProvesOnlyItsActualDataflowAndKeepsOriginalSnapshotUntouched()
    {
        var encoded = ChineseEncodedEchoIntakeTests.Encoded(); var decoded = ChineseEncodedEchoIntakeTests.Decoded();
        var original = CaptureForEncodedEcho(encoded);
        Assert.True(ChineseFullClientCatalogIntake.TryAttachEncodedEcho(original, encoded, new byte[12],
            Chinese7935EchoCatalogVersion.Catalog7931, SHA256.HashData(decoded), out var attached, out var failure), failure.ToString());
        var linked = Assert.IsType<ChineseFullClientCatalogSnapshot>(attached);
        Assert.Null(original.DecodedEchoEvidence); Assert.Equal(ChineseFullClientEchoEvidenceSource.None, original.EchoEvidenceSource);
        Assert.Equal(ChineseFullClientEchoEvidenceSource.ExplicitKeyTransform, linked.EchoEvidenceSource);
        Assert.Equal(99U, linked.DecodedEchoEvidence!.Rows[0].ExtraAttributeValue);
        Assert.Contains("Explicit-key Echo transform", ChineseFullClientCatalogIntake.WriteFindingsMarkdown(linked));
        Assert.Contains("Native key/cache ownership and activation remain unproved", ChineseFullClientCatalogIntake.WriteFindingsMarkdown(linked));

        Assert.True(ChineseFullClientCatalogIntake.TryAttachDecodedEchoEvidence(linked, SHA256.HashData(encoded), decoded,
            Chinese7935EchoCatalogVersion.Catalog7931, SHA256.HashData(decoded), out var unlinked, out _));
        var supplement = Assert.IsType<ChineseFullClientCatalogSnapshot>(unlinked);
        Assert.Equal(ChineseFullClientEchoEvidenceSource.UnlinkedDecodedSupplement, supplement.EchoEvidenceSource);
        Assert.DoesNotContain("Explicit-key Echo transform", ChineseFullClientCatalogIntake.WriteFindingsMarkdown(supplement));
        Assert.Equal(ChineseFullClientEchoEvidenceSource.ExplicitKeyTransform, linked.EchoEvidenceSource);
    }

    [Fact]
    public void AForeignRawImageOrClientVersionCannotBorrowTheCapturedEchoRelationship()
    {
        var encoded = ChineseEncodedEchoIntakeTests.Encoded(); var captured = CaptureForEncodedEcho(encoded);
        var changed = encoded.ToArray(); changed[0] ^= 1;
        Assert.False(ChineseFullClientCatalogIntake.TryAttachEncodedEcho(captured, changed, new byte[12],
            Chinese7935EchoCatalogVersion.Catalog7931, SHA256.HashData(ChineseEncodedEchoIntakeTests.Decoded()),
            out var rejected, out var failure));
        Assert.Null(rejected); Assert.Equal(Chinese7935EchoCatalogFailure.PinMismatch, failure);
        Assert.False(ChineseFullClientCatalogIntake.TryAttachEncodedEcho(CaptureForEncodedEcho(encoded, 7935), encoded, new byte[12],
            Chinese7935EchoCatalogVersion.Catalog7931, SHA256.HashData(ChineseEncodedEchoIntakeTests.Decoded()), out rejected, out failure));
        Assert.Null(rejected); Assert.Equal(Chinese7935EchoCatalogFailure.UnsupportedVersion, failure);
    }
}
