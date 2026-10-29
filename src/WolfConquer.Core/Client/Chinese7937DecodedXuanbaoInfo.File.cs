using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    /// <summary>Explicit pinned decoded input only. File identity grants no runtime or cost authority.</summary>
    public static bool TryLoadDecodedFile(string decodedPath, Chinese7937XuanbaoInfoVersion declaredVersion,
        ReadOnlySpan<byte> expectedSha256, out Chinese7937XuanbaoInfoSnapshot? snapshot,
        out Chinese7937XuanbaoInfoFailure failure)
    {
        snapshot = null;
        if (!Supported(declaredVersion)) { failure = Chinese7937XuanbaoInfoFailure.UnsupportedVersion; return false; }
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
            return TryParseDecoded(bytes, declaredVersion, out snapshot, out failure);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        { snapshot = null; failure = Chinese7937XuanbaoInfoFailure.FileRead; return false; }
    }

    private static bool OrdinaryFile(string path)
    {
        var file = new FileInfo(path); file.Refresh();
        if (!file.Exists || file.LinkTarget is not null ||
            (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) return false;
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if (!parent.Exists || parent.LinkTarget is not null ||
                (parent.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) return false;
        }
        return true;
    }
}
