using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Client;

/// <summary>Strict decoded-data intake. Never evaluates Lua or activates a runtime catalog.</summary>
public static partial class Chinese7935DecodedEchoCatalog
{
    public const int MaximumBytes = 65_536;
    public const int MaximumTokens = 8_192;
    public const int MaximumIdentifierLength = 128;
    public const int MaximumMembers = 1_024;
    public const int MaximumRows = MaximumMembers / 5;
    public const string ExportName = "cq_xuanbao_huying_extra_attr";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static bool TryParseDecoded(ReadOnlySpan<byte> decodedUtf8, Chinese7935EchoCatalogVersion declaredVersion,
        out Chinese7935EchoCatalogSnapshot? snapshot, out Chinese7935EchoCatalogFailure failure)
    {
        snapshot = null;
        if (!Supported(declaredVersion)) { failure = Chinese7935EchoCatalogFailure.UnsupportedVersion; return false; }
        if (decodedUtf8.Length is <= 0 or > MaximumBytes)
        { failure = Chinese7935EchoCatalogFailure.InputBounds; return false; }
        var captured = decodedUtf8.ToArray();
        try
        {
            ReadOnlySpan<byte> content = captured;
            if (content.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) content = content[3..];
            var text = StrictUtf8.GetString(content);
            var rows = new LiteralReader(text).Parse();
            snapshot = new(declaredVersion, Convert.ToHexString(SHA256.HashData(captured)).ToLowerInvariant(), rows);
            failure = Chinese7935EchoCatalogFailure.None; return true;
        }
        catch (DecoderFallbackException) { failure = Chinese7935EchoCatalogFailure.InvalidUtf8; return false; }
        catch (LiteralRefusal error) { failure = error.Failure; return false; }
    }

    private static bool Supported(Chinese7935EchoCatalogVersion version) =>
        version is Chinese7935EchoCatalogVersion.Catalog7930 or Chinese7935EchoCatalogVersion.Catalog7931;

    private sealed class LiteralRefusal(Chinese7935EchoCatalogFailure failure) : Exception
    {
        internal Chinese7935EchoCatalogFailure Failure { get; } = failure;
    }
}
