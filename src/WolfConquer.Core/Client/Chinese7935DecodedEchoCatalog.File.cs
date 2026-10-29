using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public static partial class Chinese7935DecodedEchoCatalog
{
    /// <summary>Explicit already-decoded input only; a supplied hash is comparison evidence, not activation authority.</summary>
    public static bool TryLoadDecodedFile(string decodedPath, Chinese7935EchoCatalogVersion declaredVersion,
        ReadOnlySpan<byte> expectedSha256, out Chinese7935EchoCatalogSnapshot? snapshot,
        out Chinese7935EchoCatalogFailure failure)
    {
        snapshot = null;
        if (!Supported(declaredVersion)) { failure = Chinese7935EchoCatalogFailure.UnsupportedVersion; return false; }
        if (expectedSha256.Length != SHA256.HashSizeInBytes)
        { failure = Chinese7935EchoCatalogFailure.PinRequired; return false; }
        if (string.IsNullOrWhiteSpace(decodedPath) || !Path.IsPathFullyQualified(decodedPath))
        { failure = Chinese7935EchoCatalogFailure.PathRequired; return false; }
        var expected = expectedSha256.ToArray();
        try
        {
            var path = Path.GetFullPath(decodedPath);
            if (OperatingSystem.IsWindows() && path.StartsWith("\\\\", StringComparison.Ordinal))
            { failure = Chinese7935EchoCatalogFailure.UnsafePath; return false; }
            if (!OrdinaryFile(path)) { failure = Chinese7935EchoCatalogFailure.UnsafePath; return false; }
            using var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (lease.Length is <= 0 or > MaximumBytes)
            { failure = Chinese7935EchoCatalogFailure.InputBounds; return false; }
            var bytes = new byte[checked((int)lease.Length)]; lease.ReadExactly(bytes);
            if (lease.ReadByte() != -1 || lease.Length != bytes.Length || !OrdinaryFile(path))
            { failure = Chinese7935EchoCatalogFailure.SourceChanged; return false; }
            if (!CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(bytes)))
            { failure = Chinese7935EchoCatalogFailure.PinMismatch; return false; }
            return TryParseDecoded(bytes, declaredVersion, out snapshot, out failure);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        { snapshot = null; failure = Chinese7935EchoCatalogFailure.FileRead; return false; }
    }

    private static bool OrdinaryFile(string path)
    {
        var file = new FileInfo(path); file.Refresh();
        if (!file.Exists || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) return false;
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0) return false;
        }
        return true;
    }
}
