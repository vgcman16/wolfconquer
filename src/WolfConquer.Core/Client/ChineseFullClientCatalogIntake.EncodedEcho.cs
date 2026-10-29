namespace WolfConquer.Core.Client;

public static partial class ChineseFullClientCatalogIntake
{
    public static bool TryAttachEncodedEcho(ChineseFullClientCatalogSnapshot captured,
        ReadOnlySpan<byte> encodedEcho, ReadOnlySpan<byte> explicitKey,
        Chinese7935EchoCatalogVersion declaredSchema, ReadOnlySpan<byte> expectedDecodedSha256,
        out ChineseFullClientCatalogSnapshot? result, out Chinese7935EchoCatalogFailure failure)
    {
        ArgumentNullException.ThrowIfNull(captured); result = null;
        if (captured.ClientVersion != 7937)
        { failure = Chinese7935EchoCatalogFailure.UnsupportedVersion; return false; }
        var selected = captured.Findings.Single(row => row.RelativePath == EchoPath);
        if (!Chinese7935DecodedEchoCatalog.TryParseEncoded(encodedEcho, explicitKey, declaredSchema,
                Convert.FromHexString(selected.Sha256), expectedDecodedSha256, out var evidence, out failure)) return false;
        result = new(captured.ClientVersion, captured.VersionSha256, captured.Findings, evidence,
            ChineseFullClientEchoEvidenceSource.ExplicitKeyTransform);
        return true;
    }
}
