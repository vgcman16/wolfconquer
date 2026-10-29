using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public static partial class Chinese7935DecodedEchoCatalog
{
    /// <summary>Explicit-key byte transform and complete data schema only; no key discovery or runtime ownership.</summary>
    public static bool TryParseEncoded(ReadOnlySpan<byte> encoded, ReadOnlySpan<byte> explicitKey,
        Chinese7935EchoCatalogVersion declaredSchema, ReadOnlySpan<byte> expectedEncodedSha256,
        ReadOnlySpan<byte> expectedDecodedSha256, out Chinese7935EchoCatalogSnapshot? snapshot,
        out Chinese7935EchoCatalogFailure failure)
    {
        snapshot = null;
        if (!Supported(declaredSchema)) { failure = Chinese7935EchoCatalogFailure.UnsupportedVersion; return false; }
        if (explicitKey.Length != 12) { failure = Chinese7935EchoCatalogFailure.KeyRequired; return false; }
        if (expectedEncodedSha256.Length != 32 || expectedDecodedSha256.Length != 32)
        { failure = Chinese7935EchoCatalogFailure.PinRequired; return false; }
        if (encoded.Length is <= 0 or > MaximumBytes)
        { failure = Chinese7935EchoCatalogFailure.InputBounds; return false; }
        var captured = encoded.ToArray(); var encodedPin = expectedEncodedSha256.ToArray();
        var decodedPin = expectedDecodedSha256.ToArray(); var key = explicitKey.ToArray();
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(encodedPin, SHA256.HashData(captured)))
            { failure = Chinese7935EchoCatalogFailure.PinMismatch; return false; }
            var decoded = ChineseLuaCatalogTransform.Decode(captured, key, MaximumBytes);
            if (!CryptographicOperations.FixedTimeEquals(decodedPin, SHA256.HashData(decoded)))
            { failure = Chinese7935EchoCatalogFailure.PinMismatch; return false; }
            return TryParseDecoded(decoded, declaredSchema, out snapshot, out failure);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
