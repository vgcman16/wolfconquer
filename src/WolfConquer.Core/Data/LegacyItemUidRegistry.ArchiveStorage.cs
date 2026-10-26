namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private static void WriteArchiveStage(RegistryPaths paths, ArchiveEntry entry)
    {
        EnsureOrdinaryPath(paths.ArchivePending);
        using var stream = new FileStream(paths.ArchivePending, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(entry.Bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void PublishArchiveStage(RegistryPaths paths, ArchiveEntry entry)
    {
        EnsureOrdinaryPath(paths.History);
        Directory.CreateDirectory(paths.History);
        EnsureOrdinaryPath(paths.History);
        var target = ArchivePath(paths, entry.Record.OperationId);
        EnsureOrdinaryPath(target);
        // Immutable publication. Neither a duplicate nor an unknown file is overwritten.
        File.Move(paths.ArchivePending, target, overwrite: false);
    }

    private static ArchiveEntry? RecoverArchiveStage(RegistryPaths paths, RegistryState current,
        byte[] beforeBytes, uint observedMaximum, ref RegistryState? pending)
    {
        var catalog = ReadArchiveCatalog(paths, current, beforeBytes, allowNext: true);
        if (catalog.Next is not { } next) return null;
        var after = RetiredState(current, next);
        if (after.HighWater < observedMaximum)
            throw new FormatException("UID retirement cannot normalize an unregistered raw UID.");
        if (pending is not null && !IsExactRetirement(current, beforeBytes, pending, next))
            throw new FormatException("UID pending state differs from its exact retirement stage.");
        if (catalog.Staged) PublishArchiveStage(paths, next);
        if (pending is null)
        {
            WritePending(paths.Pending, after);
            pending = after;
        }
        return next;
    }

    private bool TryPublishArchive(RegistryState before, byte[] beforeBytes, RegistryState after,
        ArchiveEntry entry, LegacyItemUidArchiveInterruption interruption, out string reason)
    {
        reason = RecoveryRequired + ":archivePublicationUnproven";
        try
        {
            if (!TryValidateDirectory(paths, out _) || File.Exists(paths.Pending) || File.Exists(paths.ArchivePending) ||
                !ReadBounded(paths.State).AsSpan().SequenceEqual(beforeBytes) ||
                !IsExactRetirement(before, beforeBytes, after, entry)) return false;
            _ = ReadArchiveCatalog(paths, before, null, allowNext: false);
            WriteArchiveStage(paths, entry);
            if (interruption == LegacyItemUidArchiveInterruption.AfterArchivePendingFlush)
            { reason = RecoveryRequired + ":archiveAfterPendingFlush"; return false; }
            PublishArchiveStage(paths, entry);
            if (interruption == LegacyItemUidArchiveInterruption.AfterArchivePublish)
            { reason = RecoveryRequired + ":archiveAfterPublish"; return false; }
            WritePending(paths.Pending, after);
            if (interruption == LegacyItemUidArchiveInterruption.AfterPendingFlush)
            { reason = RecoveryRequired + ":archiveAfterStatePendingFlush"; return false; }
            ReplacePending(paths);
            if (interruption == LegacyItemUidArchiveInterruption.AfterStateReplace)
            { reason = RecoveryRequired + ":archiveAfterStateReplace"; return false; }
            if (!SameState(ReadState(paths.State), after)) return false;
            var catalog = ReadArchiveCatalog(paths, after, null, allowNext: false);
            state = after;
            RememberArchiveIndex(after, catalog);
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            // Retain every possibly durable stage. No archive failure returns IDs
            // or converts an uncertain retirement into an ordinary retry.
            reason = RecoveryRequired + ":archiveWrite:" + error.GetType().Name;
            return false;
        }
    }
}
