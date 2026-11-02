using System.Security.Cryptography;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private sealed record ItemAlignmentCapture(ClientItemCatalog7937Alignment Alignment, string ReferenceSha256, string CurrentSha256);

    private static ItemAlignmentCapture? CaptureItemAlignment(string referencePath, byte[] referencePin,
        string currentPath, byte[] currentPin, out string failure)
    {
        FileStream? referenceLease = null, currentLease = null;
        ItemAlignmentCapture? prepared = null;
        Exception? cleanup = null;
        failure = "InputRead";
        try
        {
            referencePath = ConfigDecodeFullPath(referencePath); currentPath = ConfigDecodeFullPath(currentPath);
            referenceLease = OpenConfigDecodeInput(referencePath); currentLease = OpenConfigDecodeInput(currentPath);
            var referenceBytes = ReadConfigDecodeBytes(referenceLease, ClientItemCatalog7937Alignment.MaximumInputBytes);
            var currentBytes = ReadConfigDecodeBytes(currentLease, ClientItemCatalog7937Alignment.MaximumInputBytes);
            var referenceHash = SHA256.HashData(referenceBytes); var currentHash = SHA256.HashData(currentBytes);
            failure = "PinMismatch";
            if (!CryptographicOperations.FixedTimeEquals(referencePin, referenceHash) ||
                !CryptographicOperations.FixedTimeEquals(currentPin, currentHash)) return null;
            failure = "ReferenceParser";
            var reference = ClientItemCatalogDocument.Parse(referenceBytes);
            failure = "CurrentParser";
            var current = Chinese7937ItemCatalogDocument.ParseDecoded(currentBytes, 7937);
            if (!ClientItemCatalog7937Alignment.TryAnalyze(reference, current, out var alignment, out var alignmentFailure) || alignment is null)
            { failure = "Alignment:" + alignmentFailure; return null; }
            failure = "SourceChanged";
            RequireItemAlignmentCaptureCurrent(referenceLease, referencePath, referenceBytes.Length);
            RequireItemAlignmentCaptureCurrent(currentLease, currentPath, currentBytes.Length);
            prepared = new(alignment, Convert.ToHexString(referenceHash), Convert.ToHexString(currentHash));
        }
        catch (ConfigDecodeOperationException error)
        {
            if (error.Cleanup is not null) failure += "+CleanupFailed";
        }
        catch (Exception)
        {
            // Keep only the static stage. Both parser exception messages and
            // ordinary-file diagnostics may contain identities or paths.
        }
        finally
        {
            DisposeConfigDecodeLease(currentLease, ref cleanup);
            DisposeConfigDecodeLease(referenceLease, ref cleanup);
            if (cleanup is not null) failure = prepared is null ? failure + "+CleanupFailed" : "CleanupFailed";
        }
        if (cleanup is not null) return null;
        if (prepared is not null) failure = "None";
        return prepared;
    }

    private static void RequireItemAlignmentCaptureCurrent(FileStream lease, string path, int length)
    {
        ValidateConfigDecodeInput(path);
        RequireConfigDecodeOrdinaryLease(lease);
        if (lease.ReadByte() != -1 || lease.Length != length) throw new IOException("Catalog capture changed.");
    }
}
