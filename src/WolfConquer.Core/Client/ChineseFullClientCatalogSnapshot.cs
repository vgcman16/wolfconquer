using System.Collections.ObjectModel;

namespace WolfConquer.Core.Client;

public enum ChineseFullClientCatalogState
{
    ParsedKnownItemSchema,
    UnsupportedItemSchema,
    OpaqueRequiresExplicitDecoder,
    ParsedChinese7937ItemEnvelope,
}

/// <summary>Captured data observations; neither a runtime binding nor a publisher attestation.</summary>
public sealed class ChineseFullClientCatalogFinding
{
    internal ChineseFullClientCatalogFinding(string relativePath, int length, string sha256,
        ChineseFullClientCatalogState state, ClientItemCatalogDocument? items, string? decodedSha256,
        Chinese7937ItemCatalogDocument? chinese7937Items = null)
    {
        RelativePath = relativePath; Length = length; Sha256 = sha256; State = state;
        Items = items; DecodedSha256 = decodedSha256; Chinese7937Items = chinese7937Items;
    }

    public string RelativePath { get; }
    public int Length { get; }
    public string Sha256 { get; }
    public ChineseFullClientCatalogState State { get; }
    public ClientItemCatalogDocument? Items { get; }
    public Chinese7937ItemCatalogDocument? Chinese7937Items { get; }
    public string? DecodedSha256 { get; }
}

public sealed class ChineseFullClientCatalogSnapshot
{
    private readonly ReadOnlyCollection<ChineseFullClientCatalogFinding> findings;

    internal ChineseFullClientCatalogSnapshot(int clientVersion, string versionSha256,
        IEnumerable<ChineseFullClientCatalogFinding> findings,
        Chinese7935EchoCatalogSnapshot? decodedEchoEvidence = null)
    {
        ClientVersion = clientVersion; VersionSha256 = versionSha256;
        this.findings = Array.AsReadOnly(findings.ToArray());
        DecodedEchoEvidence = decodedEchoEvidence;
    }

    public int ClientVersion { get; }
    public string VersionSha256 { get; }
    public IReadOnlyList<ChineseFullClientCatalogFinding> Findings => findings;

    /// <summary>Explicit decoded supplement; its derivation from the raw Echo file is not proven by hashes.</summary>
    public Chinese7935EchoCatalogSnapshot? DecodedEchoEvidence { get; }
    public bool HasUnsupportedItems => findings.Any(row => row.State == ChineseFullClientCatalogState.UnsupportedItemSchema);
}
