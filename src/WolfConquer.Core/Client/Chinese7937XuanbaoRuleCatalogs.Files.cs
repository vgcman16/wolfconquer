using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public sealed partial class Chinese7937XuanbaoRuleCatalogs
{
    /// <summary>Two explicit pinned ordinary files; read leases are not a hostile namespace/atomic multi-file snapshot proof.</summary>
    public static bool TryLoadPackedFiles(string composePath, string randomPath, int declaredVersion,
        ReadOnlySpan<byte> composePin, ReadOnlySpan<byte> randomPin,
        out Chinese7937XuanbaoRuleCatalogs? catalogs, out Chinese7937XuanbaoRulesFailure failure)
    {
        catalogs = null;
        if (declaredVersion != 7937) { failure = Chinese7937XuanbaoRulesFailure.UnsupportedVersion; return false; }
        if (composePin.Length != SHA256.HashSizeInBytes || randomPin.Length != SHA256.HashSizeInBytes)
        { failure = Chinese7937XuanbaoRulesFailure.PinRequired; return false; }
        if (string.IsNullOrWhiteSpace(composePath) || !Path.IsPathFullyQualified(composePath) ||
            string.IsNullOrWhiteSpace(randomPath) || !Path.IsPathFullyQualified(randomPath))
        { failure = Chinese7937XuanbaoRulesFailure.PathRequired; return false; }
        var expectedCompose = composePin.ToArray(); var expectedRandom = randomPin.ToArray();
        try
        {
            var compose = Path.GetFullPath(composePath); var random = Path.GetFullPath(randomPath);
            if (!OrdinaryFile(compose) || !OrdinaryFile(random))
            { failure = Chinese7937XuanbaoRulesFailure.UnsafePath; return false; }
            using var composeLease = new FileStream(compose, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var randomLease = new FileStream(random, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!TryCapture(composeLease, out var composeBytes) || !TryCapture(randomLease, out var randomBytes))
            { failure = Chinese7937XuanbaoRulesFailure.InputBounds; return false; }
            if (composeLease.ReadByte() != -1 || randomLease.ReadByte() != -1 ||
                composeLease.Length != composeBytes.Length || randomLease.Length != randomBytes.Length ||
                !OrdinaryFile(compose) || !OrdinaryFile(random))
            { failure = Chinese7937XuanbaoRulesFailure.SourceChanged; return false; }
            return TryParsePacked(declaredVersion, composeBytes, randomBytes, expectedCompose, expectedRandom, out catalogs, out failure);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException)
        { catalogs = null; failure = Chinese7937XuanbaoRulesFailure.FileRead; return false; }
    }

    private static bool TryCapture(FileStream lease, out byte[] bytes)
    {
        bytes = [];
        var length = lease.Length;
        if (length is <= 0 or > MaximumPackedBytes) return false;
        bytes = new byte[checked((int)length)];
        lease.ReadExactly(bytes);
        return true;
    }

    private static bool OrdinaryFile(string path)
    {
        if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)) return false;
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
