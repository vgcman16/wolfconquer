using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ChineseFullClientCatalogIntakeTests
{
    private static byte[] EchoModule(string value = "4294967295") => Encoding.UTF8.GetBytes(
        "local ret = {}; ret.cq_xuanbao_huying_extra_attr = { [1] = { attr_type = 17, attr_value = 42, " +
        "extra_attr_type = 6, extra_attr_value = " + value + " } }; return ret;");

    private static byte[] RawEchoPin(ChineseFullClientCatalogSnapshot snapshot) => Convert.FromHexString(
        snapshot.Findings.Single(row => row.RelativePath == ChineseFullClientCatalogIntake.EchoPath).Sha256);

    [Fact]
    public void ExplicitDecodedEvidenceKeepsClientAndSchemaVersionsDistinctAndOwnsTheRows()
    {
        var original = Capture(); var decoded = EchoModule();
        Assert.True(ChineseFullClientCatalogIntake.TryAttachDecodedEchoEvidence(original, RawEchoPin(original), decoded,
            Chinese7935EchoCatalogVersion.Catalog7931, SHA256.HashData(decoded), out var enriched, out var failure), failure.ToString());
        var result = Assert.IsType<ChineseFullClientCatalogSnapshot>(enriched); Array.Clear(decoded);
        Assert.Null(original.DecodedEchoEvidence); Assert.Equal(7937, result.ClientVersion);
        Assert.Equal(Chinese7935EchoCatalogVersion.Catalog7931, result.DecodedEchoEvidence!.DeclaredVersion);
        Assert.Equal(new Chinese7935EchoCatalogRow(1, 17, 42, 6, uint.MaxValue), result.DecodedEchoEvidence.Rows[0]);
        Assert.Equal(ChineseFullClientCatalogState.OpaqueRequiresExplicitDecoder, result.Findings[2].State);
        Assert.Contains("raw derivation and native binding remain unproved", ChineseFullClientCatalogIntake.WriteFindingsMarkdown(result));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void BothRawAndDecodedPinsAreRequiredAndMismatchCannotReplaceOldData(bool rawMismatch)
    {
        var original = Capture(); var decoded = EchoModule();
        var rawPin = RawEchoPin(original); var decodedPin = SHA256.HashData(decoded);
        if (rawMismatch) rawPin[0] ^= 1; else decodedPin[0] ^= 1;
        Assert.False(ChineseFullClientCatalogIntake.TryAttachDecodedEchoEvidence(original, rawPin, decoded,
            Chinese7935EchoCatalogVersion.Catalog7931, decodedPin, out var rejected, out var failure));
        Assert.Null(rejected); Assert.Equal(Chinese7935EchoCatalogFailure.PinMismatch, failure);
        Assert.Null(original.DecodedEchoEvidence); Assert.Equal(42U, original.Findings[0].Items!.Rows[0].ItemId);
    }

    [Fact]
    public void ClientMarkerDoesNotAuthorizeAnInventedEcho7937Schema()
    {
        var original = Capture(); var decoded = EchoModule();
        Assert.False(ChineseFullClientCatalogIntake.TryAttachDecodedEchoEvidence(original, RawEchoPin(original), decoded,
            (Chinese7935EchoCatalogVersion)7937, SHA256.HashData(decoded), out var rejected, out var failure));
        Assert.Null(rejected); Assert.Equal(Chinese7935EchoCatalogFailure.UnsupportedVersion, failure);
    }

    [Theory]
    [InlineData("4294967296")] [InlineData("-1")] [InlineData("make_bonus()")]
    public void PinnedUnsupportedDecodedDataStillRefuses(string literal)
    {
        var original = Capture(); var decoded = EchoModule(literal);
        Assert.False(ChineseFullClientCatalogIntake.TryAttachDecodedEchoEvidence(original, RawEchoPin(original), decoded,
            Chinese7935EchoCatalogVersion.Catalog7931, SHA256.HashData(decoded), out var rejected, out var failure));
        Assert.Null(rejected); Assert.NotEqual(Chinese7935EchoCatalogFailure.None, failure); Assert.Null(original.DecodedEchoEvidence);
    }
}
