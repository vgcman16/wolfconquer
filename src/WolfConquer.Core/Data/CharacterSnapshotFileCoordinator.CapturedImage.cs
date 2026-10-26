namespace WolfConquer.Core.Data;

internal static partial class CharacterSnapshotFileCoordinator
{
    // The caller validates its complete bounded character image and economic baseline.
    // This owner supplies exact-image compare/replace using the SAME atomic writer,
    // not a second persistence mechanism or a callback that can accept a missing save.
    internal static bool TryReplaceCapturedImage(string path, ReadOnlyMemory<byte> before,
        ReadOnlyMemory<byte> after, out bool outcomeUncertain, out string reason)
    {
        outcomeUncertain = false;
        reason = "capturedImageBoundsInvalid";
        if (before.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes ||
            after.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        var expected = before.ToArray();
        var candidate = after.ToArray();
        var uncertain = false;
        var failure = string.Empty;
        var saved = ExecuteLocked(path, () => ReplaceCapturedImageLocked(path, expected, candidate, out uncertain, out failure));
        outcomeUncertain = uncertain;
        reason = failure;
        return saved;
    }

    private static bool ReplaceCapturedImageLocked(string path, byte[] before, byte[] after,
        out bool uncertain, out string reason)
    {
        uncertain = false;
        reason = "capturedImageBaselineChanged";
        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "capturedImage", out var actual, out reason)) return false;
        if (!actual.AsSpan().SequenceEqual(before))
        { reason = "capturedImageBaselineChanged"; return false; }
        var failure = string.Empty;
        var backup = string.Empty;
        try { WriteAtomically(path, temporary => File.WriteAllBytes(temporary, after), retainBackup: true, out backup); }
        catch (Exception error) when (IsCapturedImageIoFailure(error)) { failure = error.GetType().Name; }
        try
        {
            if (CharacterSnapshotFileBaseline.TryReadBytes(path, "capturedImage", out actual, out _))
            {
                // Even if the OS reported an error, exact afterimage proves this
                // one-file outcome; publish it instead of permitting a false refund.
                if (actual.AsSpan().SequenceEqual(after))
                { CleanupCapturedImageBackup(backup); reason = string.Empty; return true; }
                if (actual.AsSpan().SequenceEqual(before))
                { CleanupCapturedImageBackup(backup); reason = "capturedImageUnchanged:" + failure; return false; }
            }
        }
        catch (Exception error) when (IsCapturedImageIoFailure(error)) { }
        uncertain = true;
        // Keep any real backup retained by the writer for explicit reconciliation.
        reason = "capturedImageOutcomeUnproven:" + failure;
        return false;
    }

    private static void CleanupCapturedImageBackup(string backup)
    {
        // Cleanup cannot reverse a byte-verified outcome. Leave evidence in place
        // if this best-effort deletion is denied, rather than misclassify the save.
        try { TryDeleteTransactionFile(backup); }
        catch (Exception error) when (IsCapturedImageIoFailure(error)) { }
    }

    private static bool IsCapturedImageIoFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or System.Security.SecurityException;
}
