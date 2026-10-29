using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

/// <summary>Data-only selected intake. Does not execute, install, infer keys or activate client features.</summary>
public static partial class ChineseFullClientCatalogIntake
{
    public const int MaximumItemBytes = 64 * 1024 * 1024;
    public const int MaximumOpaqueBytes = 1024 * 1024;
    public const string MainItemsPath = "ini/itemtype.dat";
    public const string SubItemsPath = "ini/ItemtypeSub.dat";
    public const string EchoPath = "ini/luacfg/XuanBaoEcho.dat";
    public const string EchoConfigurationPath = "ini/luacfg/XuanBaoInfo.dat";

    public static ChineseFullClientCatalogSnapshot ParseCaptured(int expectedClientVersion,
        ReadOnlySpan<byte> version, ReadOnlySpan<byte> mainItems, ReadOnlySpan<byte> subItems,
        ReadOnlySpan<byte> packedEcho, ReadOnlySpan<byte> packedEchoConfiguration)
    {
        var versionBytes = Capture(version, 64);
        var observedVersion = ReadVersion(versionBytes);
        if (expectedClientVersion is <= 0 or > 9999 || observedVersion != expectedClientVersion)
            throw new InvalidDataException("Explicit client version does not match the captured marker.");
        var main = Capture(mainItems, MaximumItemBytes); var sub = Capture(subItems, MaximumItemBytes);
        var echo = Capture(packedEcho, MaximumOpaqueBytes); var configuration = Capture(packedEchoConfiguration, MaximumOpaqueBytes);
        return new(observedVersion, Digest(versionBytes), new[]
        {
            InspectItems(MainItemsPath, main, observedVersion), InspectItems(SubItemsPath, sub, observedVersion),
            Opaque(EchoPath, echo), Opaque(EchoConfigurationPath, configuration),
        });
    }

    public static bool TryAttachDecodedEchoEvidence(ChineseFullClientCatalogSnapshot captured,
        ReadOnlySpan<byte> expectedPackedEchoSha256, ReadOnlySpan<byte> decodedEcho,
        Chinese7935EchoCatalogVersion declaredSchema, ReadOnlySpan<byte> expectedDecodedSha256,
        out ChineseFullClientCatalogSnapshot? result, out Chinese7935EchoCatalogFailure failure)
    {
        ArgumentNullException.ThrowIfNull(captured); result = null;
        if (expectedPackedEchoSha256.Length != 32 || expectedDecodedSha256.Length != 32)
        { failure = Chinese7935EchoCatalogFailure.PinRequired; return false; }
        if (decodedEcho.Length is <= 0 or > Chinese7935DecodedEchoCatalog.MaximumBytes)
        { failure = Chinese7935EchoCatalogFailure.InputBounds; return false; }
        var decoded = decodedEcho.ToArray();
        var raw = captured.Findings.Single(row => row.RelativePath == EchoPath);
        if (!CryptographicOperations.FixedTimeEquals(expectedPackedEchoSha256, Convert.FromHexString(raw.Sha256)) ||
            !CryptographicOperations.FixedTimeEquals(expectedDecodedSha256, SHA256.HashData(decoded)))
        { failure = Chinese7935EchoCatalogFailure.PinMismatch; return false; }
        if (!Chinese7935DecodedEchoCatalog.TryParseDecoded(decoded, declaredSchema, out var evidence, out failure)) return false;
        result = new(captured.ClientVersion, captured.VersionSha256, captured.Findings, evidence);
        return true;
    }

    private static byte[] Capture(ReadOnlySpan<byte> bytes, int maximum)
    {
        if (bytes.Length is <= 0 || bytes.Length > maximum)
            throw new InvalidDataException("Selected input exceeds the declared nonempty bounds.");
        return bytes.ToArray();
    }

    private static int ReadVersion(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 6 && bytes[4] == 13 && bytes[5] == 10) bytes = bytes[..4];
        else if (bytes.Length == 5 && bytes[4] == 10) bytes = bytes[..4];
        if (bytes.Length != 4) throw new InvalidDataException("Unsupported version marker grammar.");
        var version = 0;
        foreach (var value in bytes)
        {
            if (value is < (byte)'0' or > (byte)'9') throw new InvalidDataException("Unsupported version marker grammar.");
            version = version * 10 + value - (byte)'0';
        }
        if (version == 0) throw new InvalidDataException("A zero version is unsupported.");
        return version;
    }

    private static ChineseFullClientCatalogFinding InspectItems(string path, byte[] packed, int clientVersion)
    {
        var decoded = ClientItemCatalogCodec.Decode(packed);
        if (!packed.AsSpan().SequenceEqual(ClientItemCatalogCodec.Encode(decoded))) return UnsupportedItems(path, packed);
        try
        {
            var document = ClientItemCatalogDocument.Parse(decoded);
            if (!decoded.AsSpan().SequenceEqual(document.ToArray()))
                throw new InvalidDataException("Known item schema did not reconstruct exactly.");
            return new(path, packed.Length, Digest(packed), ChineseFullClientCatalogState.ParsedKnownItemSchema,
                document, Digest(decoded));
        }
        catch (Exception error) when (error is InvalidDataException or FormatException or ArgumentException or OverflowException) { }
        if (clientVersion != Chinese7937ItemCatalogDocument.ClientVersion) return UnsupportedItems(path, packed);
        try
        {
            var document = Chinese7937ItemCatalogDocument.ParseDecoded(decoded, clientVersion);
            if (!decoded.AsSpan().SequenceEqual(document.ToArray())) return UnsupportedItems(path, packed);
            return new(path, packed.Length, Digest(packed), ChineseFullClientCatalogState.ParsedChinese7937ItemEnvelope,
                null, Digest(decoded), document);
        }
        catch (Exception error) when (error is InvalidDataException or FormatException or ArgumentException or OverflowException)
        { return UnsupportedItems(path, packed); }
    }

    private static ChineseFullClientCatalogFinding UnsupportedItems(string path, byte[] packed) =>
        new(path, packed.Length, Digest(packed), ChineseFullClientCatalogState.UnsupportedItemSchema, null, null);

    private static ChineseFullClientCatalogFinding Opaque(string path, byte[] bytes) =>
        new(path, bytes.Length, Digest(bytes), ChineseFullClientCatalogState.OpaqueRequiresExplicitDecoder, null, null);

    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
