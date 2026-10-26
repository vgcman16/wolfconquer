namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private static bool TryCreate(RegistryPaths paths, RegistryState initial,
        LegacyItemUidInterruption interruption, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (File.Exists(paths.State) || File.Exists(paths.Pending))
            { reason = RecoveryRequired + ":bootstrapStateAppeared"; return false; }
            WritePending(paths.Pending, initial);
            if (interruption == LegacyItemUidInterruption.AfterPendingFlush)
            { reason = RecoveryRequired + ":bootstrapAfterFlush"; return false; }
            ReplacePending(paths);
            if (interruption == LegacyItemUidInterruption.AfterStateReplace)
            { reason = RecoveryRequired + ":bootstrapAfterReplace"; return false; }
            if (!SameState(ReadState(paths.State), initial))
            { reason = RecoveryRequired + ":bootstrapPoststateUnproven"; return false; }
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        { reason = RecoveryRequired + ":bootstrapWrite:" + exception.GetType().Name; return false; }
    }

    private static bool TryAdvance(RegistryPaths paths, RegistryState before, RegistryState after,
        LegacyItemUidInterruption interruption, out string reason)
    {
        reason = string.Empty;
        byte[]? beforeBytes = null;
        try
        {
            if (!TryValidateDirectory(paths, out reason) || File.Exists(paths.Pending))
            { reason = RecoveryRequired + ":pendingOrUnexpectedEntry"; return false; }
            beforeBytes = ReadBounded(paths.State);
            if (!SameState(ParseState(beforeBytes), before))
            { reason = RecoveryRequired + ":stateChanged"; return false; }
            // Archive membership is unchanged by an ordinary reservation. The
            // quiescent owner already admitted its immutable hash index.
            if (before.ArchiveCount.GetValueOrDefault() != 0 && !Directory.Exists(paths.History))
                throw new FormatException("UID archive root is missing.");
            WritePending(paths.Pending, after);
            if (interruption == LegacyItemUidInterruption.AfterPendingFlush)
            { reason = RecoveryRequired + ":reservationAfterFlush"; return false; }
            ReplacePending(paths);
            if (interruption == LegacyItemUidInterruption.AfterStateReplace)
            { reason = RecoveryRequired + ":reservationAfterReplace"; return false; }
            if (!SameState(ReadState(paths.State), after))
            { reason = RecoveryRequired + ":reservationPoststateUnproven"; return false; }
            return true;
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            // Never delete a possibly durable pending reservation or decrement
            // the high water. An ordinary refusal requires the exact old bytes.
            try
            {
                if (beforeBytes is not null && !File.Exists(paths.Pending) && !Directory.Exists(paths.Pending) &&
                    ReadBounded(paths.State).AsSpan().SequenceEqual(beforeBytes))
                { reason = "itemUidWriteRejected:" + exception.GetType().Name; return false; }
            }
            catch (Exception readError) when (IsFileFailure(readError)) { }
            reason = RecoveryRequired + ":reservationWriteUnproven:" + exception.GetType().Name;
            return false;
        }
    }

    private static bool TryRecoverAndRead(RegistryPaths paths, LegacyItemUidRegistryOptions options,
        uint observedMaximum, out RegistryState state, out string reason)
    {
        state = null!;
        reason = RecoveryRequired + ":registryStateMissing";
        var hasState = File.Exists(paths.State);
        var hasPending = File.Exists(paths.Pending);
        if (!hasState && !hasPending) return false;
        var currentBytes = hasState ? ReadBounded(paths.State) : null;
        var current = currentBytes is not null ? ParseState(currentBytes) : null;
        var pending = hasPending ? ReadState(paths.Pending) : null;
        if ((current is not null && !MatchesConfiguration(current, paths, options)) ||
            (pending is not null && !MatchesConfiguration(pending, paths, options)))
        { reason = RecoveryRequired + ":configurationChanged"; return false; }
        ArchiveEntry? retiring = null;
        if (current is not null) retiring = RecoverArchiveStage(paths, current, currentBytes!, observedMaximum, ref pending);
        else if (File.Exists(paths.ArchivePending) || Directory.Exists(paths.History))
        { reason = RecoveryRequired + ":orphanArchive"; return false; }
        if (pending is not null)
        {
            if (current is null)
            {
                if (pending.Generation != 0)
                { reason = RecoveryRequired + ":orphanReservation"; return false; }
            }
            else if (!SameState(pending, current) && !IsNext(current, pending) &&
                (retiring is null || !IsExactRetirement(current, currentBytes!, pending, retiring)))
            { reason = RecoveryRequired + ":pendingChainInvalid"; return false; }
            if (pending.HighWater < observedMaximum)
            { reason = RecoveryRequired + ":unregisteredDataUid"; return false; }
            if (SameState(pending, current))
            {
                EnsureOrdinaryPath(paths.Pending);
                File.Delete(paths.Pending);
            }
            else ReplacePending(paths);
            current = ReadState(paths.State);
            if (!SameState(current, pending))
            { reason = RecoveryRequired + ":recoveryPoststateUnproven"; return false; }
        }
        if (current is null || current.HighWater < observedMaximum)
        { reason = RecoveryRequired + ":unregisteredDataUid"; return false; }
        _ = ReadArchiveCatalog(paths, current, null, allowNext: false);
        state = current;
        reason = string.Empty;
        return true;
    }

    private static bool MatchesConfiguration(RegistryState state, RegistryPaths paths,
        LegacyItemUidRegistryOptions options) =>
        PathComparer.Equals(state.Users, paths.Users) && PathComparer.Equals(state.Items, paths.Items) &&
        PathComparer.Equals(state.Mailbox, paths.Mailbox) && PathComparer.Equals(state.Redeem, paths.Redeem) &&
        PathComparer.Equals(state.Claim, paths.Claim) && state.HistoricalFloor == options.HistoricalHighWater &&
        string.Equals(state.HistoricalEvidence, options.HistoricalEvidence, StringComparison.Ordinal);

    private static void WritePending(string path, RegistryState state)
    {
        EnsureOrdinaryPath(path);
        var bytes = SerializeState(state);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 4096, options: FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void ReplacePending(RegistryPaths paths)
    {
        EnsureOrdinaryPath(paths.State);
        EnsureOrdinaryPath(paths.Pending);
        if (File.Exists(paths.State)) File.Replace(paths.Pending, paths.State, null, ignoreMetadataErrors: true);
        else File.Move(paths.Pending, paths.State);
    }
}
