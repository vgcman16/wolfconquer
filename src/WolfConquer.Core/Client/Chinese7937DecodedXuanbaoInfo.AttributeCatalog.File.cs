using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    /// <summary>Reads explicit pinned plain data only; ordinary-file checks do not prove hostile namespace stability.</summary>
    public static bool TryLoadAttributeCatalogFile(string decodedPath, Chinese7937XuanbaoInfoVersion declaredVersion,
        ReadOnlySpan<byte> expectedSha256, out Chinese7937XuanbaoAttributeCatalog? catalog,
        out Chinese7937XuanbaoInfoFailure failure)
    {
        catalog = null;
        if (declaredVersion != Chinese7937XuanbaoInfoVersion.Catalog7937)
        { failure = Chinese7937XuanbaoInfoFailure.UnsupportedVersion; return false; }
        if (expectedSha256.Length != SHA256.HashSizeInBytes)
        { failure = Chinese7937XuanbaoInfoFailure.PinRequired; return false; }
        if (string.IsNullOrWhiteSpace(decodedPath) || !Path.IsPathFullyQualified(decodedPath))
        { failure = Chinese7937XuanbaoInfoFailure.PathRequired; return false; }
        var expected = expectedSha256.ToArray();
        try
        {
            var path = Path.GetFullPath(decodedPath);
            if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) || !OrdinaryFile(path))
            { failure = Chinese7937XuanbaoInfoFailure.UnsafePath; return false; }
            using var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (lease.Length is <= 0 or > MaximumBytes)
            { failure = Chinese7937XuanbaoInfoFailure.InputBounds; return false; }
            var bytes = new byte[checked((int)lease.Length)];
            lease.ReadExactly(bytes);
            if (lease.ReadByte() != -1 || lease.Length != bytes.Length || !OrdinaryFile(path))
            { failure = Chinese7937XuanbaoInfoFailure.SourceChanged; return false; }
            if (!CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(bytes)))
            { failure = Chinese7937XuanbaoInfoFailure.PinMismatch; return false; }
            return TryParseAttributeCatalog(bytes, declaredVersion, out catalog, out failure);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        { catalog = null; failure = Chinese7937XuanbaoInfoFailure.FileRead; return false; }
    }
}
