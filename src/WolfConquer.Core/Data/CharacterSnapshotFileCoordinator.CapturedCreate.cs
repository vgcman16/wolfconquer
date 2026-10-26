namespace WolfConquer.Core.Data;

internal static partial class CharacterSnapshotFileCoordinator
{
    // First-save counterpart to captured replace. Never switches to replacement
    // if another creator wins the absent leaf. The caller owns path/graph policy.
    internal static bool TryCreateCapturedImage(string path, ReadOnlyMemory<byte> after,
        out bool outcomeUncertain, out string reason)
    {
        outcomeUncertain = false;
        reason = "capturedImageBoundsInvalid";
        if (after.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        var candidate = after.ToArray();
        var uncertain = false;
        var failure = string.Empty;
        var saved = ExecuteLocked(path, () => CreateCapturedImageLocked(path, candidate, out uncertain, out failure));
        outcomeUncertain = uncertain;
        reason = failure;
        return saved;
    }

    private static bool CreateCapturedImageLocked(string path, byte[] after, out bool uncertain, out string reason)
    {
        uncertain = false;
        reason = "capturedImageLeafAlreadyExists";
        if (!CapturedImageLeafMissing(path)) return false;
        var published = false;
        var failure = string.Empty;
        try
        {
            WriteAtomically(path, temporary => File.WriteAllBytes(temporary, after), retainBackup: false,
                createOnly: true, out _, out published);
        }
        catch (Exception error) when (IsCapturedImageIoFailure(error)) { failure = error.GetType().Name; }
        try
        {
            if (published && CharacterSnapshotFileBaseline.TryReadBytes(path, "capturedImage", out var actual, out _) &&
                actual.AsSpan().SequenceEqual(after))
            { reason = string.Empty; return true; }
            if (!published && CapturedImageLeafMissing(path))
            { reason = "capturedImageCreateUnchanged:" + failure; return false; }
        }
        catch (Exception error) when (IsCapturedImageIoFailure(error)) { }
        // Identical bytes do not prove our failed move owned a competing creator's
        // leaf. The completed-move flag is real writer evidence, not a test callback.
        uncertain = true;
        reason = "capturedImageCreateOutcomeUnproven:" + failure;
        return false;
    }

    private static bool CapturedImageLeafMissing(string path)
    {
        try { _ = File.GetAttributes(path); return false; }
        catch (FileNotFoundException) { return true; }
        // DirectoryNotFound/access/readability failures deliberately propagate;
        // neither a missing parent nor an unreadable leaf grants create authority.
    }
}
