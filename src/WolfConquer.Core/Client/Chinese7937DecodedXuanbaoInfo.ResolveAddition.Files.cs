using System.Security.Cryptography;

namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    /// <summary>Explicit pinned plain Info and packed Addition DATA only. Does not bind runtime state or authenticate provenance.</summary>
    public static bool TryLoadResolveAndAdditionFiles(string plainInfoPath, string packedAdditionPath, int declaredVersion,
        ReadOnlySpan<byte> plainInfoSha256, ReadOnlySpan<byte> packedAdditionSha256,
        out Chinese7937XuanbaoResolveSnapshot? resolve, out Chinese7937XuanbaoAdditionRawCatalog? addition, out string failure)
    {
        resolve = null; addition = null; failure = "UnsupportedVersion";
        if (declaredVersion != 7937) return false;
        if (plainInfoSha256.Length != SHA256.HashSizeInBytes || packedAdditionSha256.Length != SHA256.HashSizeInBytes)
        { failure = "PinRequired"; return false; }
        if (string.IsNullOrWhiteSpace(plainInfoPath) || !Path.IsPathFullyQualified(plainInfoPath) ||
            string.IsNullOrWhiteSpace(packedAdditionPath) || !Path.IsPathFullyQualified(packedAdditionPath))
        { failure = "PathRequired"; return false; }
        var infoPin = plainInfoSha256.ToArray(); var additionPin = packedAdditionSha256.ToArray();
        var leases = new FileStream?[2];
        Chinese7937XuanbaoResolveSnapshot? preparedResolve = null;
        Chinese7937XuanbaoAdditionRawCatalog? preparedAddition = null;
        bool prepared = false, cleanupFailed = false;
        failure = "FileRead";
        try
        {
            var infoPath = Path.GetFullPath(plainInfoPath); var additionPath = Path.GetFullPath(packedAdditionPath);
            if (!ResolveAdditionOrdinaryPath(infoPath) || !ResolveAdditionOrdinaryPath(additionPath)) failure = "UnsafePath";
            else
            {
                leases[0] = new FileStream(infoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                leases[1] = new FileStream(additionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                prepared = TryCaptureResolveAddition(leases[0]!, leases[1]!, infoPath, additionPath, infoPin, additionPin,
                    out preparedResolve, out preparedAddition, out failure);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or OverflowException or System.Security.SecurityException)
        { failure = "FileRead"; }
        finally
        {
            // Attempt both releases even if one fails. Keep the primary static
            // refusal code and append cleanup status instead of replacing it.
            foreach (var lease in leases)
            {
                try { lease?.Dispose(); }
                catch (Exception) { cleanupFailed = true; }
            }
        }
        if (cleanupFailed) { failure = prepared ? "CleanupFailed" : failure + "+CleanupFailed"; return false; }
        if (!prepared) return false;
        resolve = preparedResolve; addition = preparedAddition; failure = "None"; return true;
    }

    private static bool TryCaptureResolveAddition(FileStream infoLease, FileStream additionLease, string infoPath, string additionPath,
        byte[] infoPin, byte[] additionPin, out Chinese7937XuanbaoResolveSnapshot? resolve,
        out Chinese7937XuanbaoAdditionRawCatalog? addition, out string failure)
    {
        resolve = null; addition = null; failure = "InputBounds";
        var infoLength = infoLease.Length; var additionLength = additionLease.Length;
        if (infoLength is <= 0 or > MaximumBytes || additionLength is <= 0 or > Chinese7937XuanbaoAdditionRawCatalog.MaximumBytes)
            return false;
        var infoBytes = new byte[checked((int)infoLength)]; var additionBytes = new byte[checked((int)additionLength)];
        infoLease.ReadExactly(infoBytes); additionLease.ReadExactly(additionBytes);
        if (!ResolveAdditionCaptureCurrent(infoLease, infoBytes.Length, infoPath) ||
            !ResolveAdditionCaptureCurrent(additionLease, additionBytes.Length, additionPath))
        { failure = "SourceChanged"; return false; }
        if (!CryptographicOperations.FixedTimeEquals(infoPin, SHA256.HashData(infoBytes)) ||
            !CryptographicOperations.FixedTimeEquals(additionPin, SHA256.HashData(additionBytes)))
        { failure = "PinMismatch"; return false; }
        if (!TryParseResolve(infoBytes, Chinese7937XuanbaoInfoVersion.Catalog7937, out var parsedResolve, out var resolveFailure) || parsedResolve is null)
        { failure = "ResolveParser:" + resolveFailure; return false; }
        if (!Chinese7937XuanbaoAdditionRawCatalog.TryParsePacked(7937, additionBytes, additionPin, out var parsedAddition, out var additionFailure) || parsedAddition is null)
        { failure = "AdditionParser:" + additionFailure; return false; }
        if (!ResolveAdditionCaptureCurrent(infoLease, infoBytes.Length, infoPath) ||
            !ResolveAdditionCaptureCurrent(additionLease, additionBytes.Length, additionPath))
        { failure = "SourceChanged"; return false; }
        resolve = parsedResolve; addition = parsedAddition; failure = "None"; return true;
    }

    private static bool ResolveAdditionCaptureCurrent(FileStream lease, int length, string path) =>
        lease.ReadByte() == -1 && lease.Length == length && ResolveAdditionOrdinaryPath(path);

    private static bool ResolveAdditionOrdinaryPath(string path) =>
        !path.StartsWith("\\\\", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal) && OrdinaryFile(path);
}
