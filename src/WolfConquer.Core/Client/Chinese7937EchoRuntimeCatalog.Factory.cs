using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public sealed partial class Chinese7937EchoRuntimeCatalog
{
    /// <summary>Validates complete encoded Info and its captured7937 item catalogs before publication.</summary>
    public static bool TryCreate(ChineseFullClientCatalogSnapshot captured, ReadOnlySpan<byte> encodedInfo,
        ReadOnlySpan<byte> explicitKey, ReadOnlySpan<byte> expectedDecodedInfoSha256,
        out Chinese7937EchoRuntimeCatalog? catalog, out Chinese7937EchoRuntimeCatalogFailure failure)
    {
        catalog = null;
        if (captured is null) { failure = Chinese7937EchoRuntimeCatalogFailure.SourceRequired; return false; }
        if (captured.ClientVersion != Chinese7937ItemCatalogDocument.ClientVersion)
        { failure = Chinese7937EchoRuntimeCatalogFailure.UnsupportedVersion; return false; }
        if (!TryGetItems(captured, ChineseFullClientCatalogIntake.MainItemsPath, out var main) ||
            !TryGetItems(captured, ChineseFullClientCatalogIntake.SubItemsPath, out var sub))
        { failure = Chinese7937EchoRuntimeCatalogFailure.ItemEnvelopeRequired; return false; }
        if (!TryGetFinding(captured, ChineseFullClientCatalogIntake.EchoConfigurationPath, out var infoFinding))
        { failure = Chinese7937EchoRuntimeCatalogFailure.InfoFindingRequired; return false; }
        if (!TryDecodeRequirements(infoFinding!, encodedInfo, explicitKey, expectedDecodedInfoSha256,
                out var requirements, out failure)) return false;

        var prepared = new Chinese7937EchoRuntimeCatalog(captured, requirements!, main!, sub!);
        if (!prepared.AllItemReferencesResolve())
        { failure = Chinese7937EchoRuntimeCatalogFailure.MissingItemReference; return false; }
        catalog = prepared;
        failure = Chinese7937EchoRuntimeCatalogFailure.None;
        return true;
    }

    private static bool TryDecodeRequirements(ChineseFullClientCatalogFinding finding,
        ReadOnlySpan<byte> encodedInfo, ReadOnlySpan<byte> explicitKey, ReadOnlySpan<byte> expectedDecodedPin,
        out Chinese7937EchoRequirements? requirements, out Chinese7937EchoRuntimeCatalogFailure failure)
    {
        requirements = null;
        if (explicitKey.Length != 12) { failure = Chinese7937EchoRuntimeCatalogFailure.KeyRequired; return false; }
        if (expectedDecodedPin.Length != 32) { failure = Chinese7937EchoRuntimeCatalogFailure.PinRequired; return false; }
        if (encodedInfo.Length is <= 0 or > Chinese7937DecodedXuanbaoInfo.MaximumBytes)
        { failure = Chinese7937EchoRuntimeCatalogFailure.InputBounds; return false; }
        var encoded = encodedInfo.ToArray();
        var decodedPin = expectedDecodedPin.ToArray();
        var key = explicitKey.ToArray();
        try
        {
            if (encoded.Length != finding.Length ||
                !CryptographicOperations.FixedTimeEquals(SHA256.HashData(encoded), Convert.FromHexString(finding.Sha256)))
            { failure = Chinese7937EchoRuntimeCatalogFailure.PinMismatch; return false; }
            var decoded = ChineseLuaCatalogTransform.Decode(encoded, key, Chinese7937DecodedXuanbaoInfo.MaximumBytes);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(decoded), decodedPin))
            { failure = Chinese7937EchoRuntimeCatalogFailure.PinMismatch; return false; }
            if (!Chinese7937DecodedXuanbaoInfo.TryParseDecoded(decoded, Chinese7937XuanbaoInfoVersion.Catalog7937,
                    out var info, out _))
            { failure = Chinese7937EchoRuntimeCatalogFailure.InvalidInfo; return false; }
            if (!Chinese7937EchoRequirements.TryCreate(info!, out requirements, out _))
            { failure = Chinese7937EchoRuntimeCatalogFailure.UnsupportedRequirements; return false; }
            failure = Chinese7937EchoRuntimeCatalogFailure.None;
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static bool TryGetItems(ChineseFullClientCatalogSnapshot captured, string path,
        out Chinese7937ItemCatalogDocument? document)
    {
        document = null;
        if (!TryGetFinding(captured, path, out var finding) ||
            finding!.State != ChineseFullClientCatalogState.ParsedChinese7937ItemEnvelope) return false;
        document = finding.Chinese7937Items;
        return document is not null;
    }

    private static bool TryGetFinding(ChineseFullClientCatalogSnapshot captured, string path,
        out ChineseFullClientCatalogFinding? result)
    {
        result = null;
        foreach (var finding in captured.Findings)
        {
            if (!string.Equals(finding.RelativePath, path, StringComparison.Ordinal)) continue;
            if (result is not null) { result = null; return false; }
            result = finding;
        }
        return result is not null;
    }
}
