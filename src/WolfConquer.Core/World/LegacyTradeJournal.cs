using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyTradeJournalFileKind { Character, Items, Quests, Mailbox, CombatGear, Spells, Proficiencies }

internal enum LegacyCharacterSpellsJournalInterruption { None, AfterPending, AfterCharacter, AfterSpells }

internal enum LegacyCharacterItemsSpellsJournalInterruption { None, AfterPending, AfterCharacter, AfterItems, AfterSpells }

internal sealed record LegacyTradeJournalChange(
    LegacyTradeJournalFileKind Kind, long FileId, byte[]? Before, byte[] After);

// One pending record per canonical Users root. Complete snapshot-file monitors
// precede the common journal domain gate; startup recovery precedes live owners.
// This is process-restart recovery, not a multi-process or power-loss transaction.
internal static partial class LegacyTradeJournal
{
    internal const string RecoveryRequired = "tradeJournalRecoveryRequired";
    private const string JournalDirectoryName = ".managed-trade-journal";
    private const string PendingName = "pending.json";
    private const int MaxTargetBytes = 16 * 1024 * 1024;
    private const int MaxJournalBytes = 192 * 1024 * 1024;

    internal static bool TryCommit(string usersRoot, string itemsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, out string reason) =>
        TryCommit(usersRoot, itemsRoot, changes, null, out reason);

    // Closed interruption plans can retain a durable prefix without rollback.
    // Boundary zero follows publication; one through four follow replacement.
    internal static bool TryCommit(string usersRoot, string itemsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes,
        LegacyJournalInterruption? interruptAfterReplacement, out string reason) =>
        TryCommitVersion(usersRoot, itemsRoot, changes, version: 1, interruptAfterReplacement, out reason);

    private static bool TryCommitVersion(string usersRoot, string itemsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, int version,
        LegacyJournalInterruption? interruptAfterReplacement, out string reason)
    {
        reason = string.Empty;
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            var roots = GetRoots(usersRoot, itemsRoot);
            var record = CreateRecord(changes, version);
            var targets = GetTargets(roots, record);
            var lockedReason = string.Empty;
            var result = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(t => t.Path).ToArray(),
                () => CommitLocked(roots, record, targets, interruptAfterReplacement, out lockedReason));
            reason = lockedReason;
            return result;
        }
        catch (Exception ex) when (IsJournalException(ex))
        {
            reason = "tradeJournalInputInvalid:" + ex.GetType().Name;
            return false;
        }
    }

    internal static bool HasPending(string usersRoot)
    {
        try
        {
            using var serialized = new JournalDomainScope(usersRoot, mutation: false);
            var root = Path.GetFullPath(usersRoot);
            EnsureSafePath(root);
            var directory = Path.Combine(root, JournalDirectoryName);
            EnsureSafePath(directory);
            return File.Exists(directory) ||
                Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any();
        }
        catch (Exception ex) when (IsJournalException(ex)) { return true; }
    }

    private static bool CommitLocked(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets, LegacyJournalInterruption? interrupt, out string reason)
    {
        var paths = targets.Select(target => target.Path).ToArray();
        interrupt?.Claim(targets.Length, record.Version);
        // Permission checks may inspect higher package/admission owners. Keep
        // them before J, under the caller's complete existing file monitors.
        if (HasPending(roots.Users)) { reason = RecoveryRequired + ":pending"; return false; }
        try
        {
            EnsureTargetsMatch(targets, allowAfter: false);
            EnsureAdmissionInputs(roots, record);
            if (record.Version == 19) RequireAuthenticatedCreationMutation(record, targets);
            else LegacyMailboxPackageMutationFence.RequireJournalMutation(paths, record.Changes);
        }
        catch (Exception error) when (IsJournalException(error))
        {
            reason = HasPending(roots.Users) ? RecoveryRequired + ":pending" : "tradeJournalWriteFailed:" + error.GetType().Name;
            return false;
        }
        using var serialized = new JournalDomainScope(roots.Users, mutation: true);
        return CommitSerialized(roots, record, targets, paths, interrupt, out reason);
    }

    private static bool CommitSerialized(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets, string[] paths, LegacyJournalInterruption? interrupt, out string reason)
    {
        reason = string.Empty;
        var published = false;
        var retainStoppedStages = false;
        var pending = Path.Combine(roots.Journal, PendingName);
        var preparing = Path.Combine(roots.Journal, record.TransactionId + ".preparing");
        try
        {
            if (HasPending(roots.Users))
            {
                reason = RecoveryRequired + ":pending";
                return false;
            }
            EnsureTargetsMatch(targets, allowAfter: false);
            EnsureAdmissionInputs(roots, record);
            RequireAuthenticatedCreationBoundary(record);
            if (!IsOrdinaryAdmissionVersion(record.Version) && record.Version is not (8 or 9 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19)) Directory.CreateDirectory(roots.Items);
            Directory.CreateDirectory(roots.Journal);
            EnsureSafePath(roots.Journal);
            WriteFlushed(preparing, Serialize(record));
            RequireAuthenticatedCreationBoundary(record);
            File.Move(preparing, pending); // No overwrite: only one transaction can publish.
            published = true;
            if (interrupt?.AtBoundary(0, paths, pending) == true)
            {
                reason = RecoveryRequired + ":interrupted";
                return false;
            }
            if (record.Version == 19) PrepareAuthenticatedCreationDirectories(roots, record, targets);
            else if (record.Version is 9 or 12 or 13 or 14 or 15 or 16 or 17 or 18) PrepareCharacterItemsSpellsDirectories(roots, record, targets);
            else if (record.Version == 8) PrepareCharacterSpellsDirectories(roots, record, targets);
            else PrepareAdmissionDirectories(roots, record, targets);
            for (var index = 0; index < targets.Length; index++)
            {
                var target = targets[index];
                RequireAuthenticatedCreationBoundary(record);
                if (!IsOrdinaryAdmissionVersion(record.Version) || !Matches(ReadTarget(target.Path), target.Change.After))
                {
                    Stage(target, target.Change.After, record.TransactionId);
                    if (interrupt?.AtStageFlushed(index + 1, paths, pending) == true)
                    {
                        retainStoppedStages = true;
                        reason = RecoveryRequired + ":stageInterrupted";
                        return false;
                    }
                }
            }
            for (var index = 0; index < targets.Length; index++)
            {
                RequireAuthenticatedCreationBoundary(record);
                if (!IsOrdinaryAdmissionVersion(record.Version) || !Matches(ReadTarget(targets[index].Path), targets[index].Change.After))
                    ReplaceStaged(targets[index], record.TransactionId);
                if (interrupt?.AtBoundary(index + 1, paths, pending) == true)
                {
                    reason = RecoveryRequired + ":interrupted";
                    return false;
                }
            }
            EnsureAllAfter(targets);
            EnsureAdmissionInputs(roots, record);
            EnsureAdmissionStagesCleared(record, targets);
            RequireAuthenticatedCreationBoundary(record);
            try { DeleteRecord(pending); }
            catch (Exception ex) when (IsJournalException(ex))
            {
                reason = RecoveryRequired + ":committedRecordDeleteFailed:" + ex.GetType().Name;
                return false;
            }
            return true;
        }
        catch (Exception ex) when (IsJournalException(ex))
        {
            if (!published)
            {
                reason = TryDelete(preparing) && !HasPending(roots.Users)
                    ? "tradeJournalWriteFailed:" + ex.GetType().Name
                    : RecoveryRequired + ":publicationFailed:" + ex.GetType().Name;
                return false;
            }
            // An exception after publication can have occurred after an OS replacement.
            // Validate every target before restoring any, then restore exact preimages.
            if (TryRollbackWithAdmissionInputs(roots, record, targets))
            {
                try
                {
                    EnsureAdmissionStagesCleared(record, targets);
                    DeleteRecord(pending);
                    reason = "tradeJournalWriteFailed:" + ex.GetType().Name;
                    return false;
                }
                catch (Exception deleteError) when (IsJournalException(deleteError)) { }
            }
            reason = RecoveryRequired + ":rollbackOrDeleteFailed:" + ex.GetType().Name;
            return false;
        }
        finally
        {
            if (!retainStoppedStages)
                foreach (var target in targets) TryDelete(StagePath(target, record.TransactionId));
        }
    }

    private static bool TryRollback(JournalTarget[] targets, string transactionId)
    {
        try
        {
            EnsureTargetsMatch(targets, allowAfter: true);
            foreach (var target in targets.Reverse())
            {
                if (Matches(ReadTarget(target.Path), target.Change.Before)) continue;
                if (target.Change.Before is null)
                {
                    EnsureSafePath(target.Path);
                    File.Delete(target.Path);
                }
                else
                {
                    TryDelete(StagePath(target, transactionId));
                    Stage(target, target.Change.Before, transactionId);
                    ReplaceStaged(target, transactionId);
                }
            }
            EnsureTargetsMatch(targets, allowAfter: false);
            return true;
        }
        catch (Exception ex) when (IsJournalException(ex)) { return false; }
    }
}
