using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyTradeJournalFileKind { Character, Items, Quests, Mailbox, CombatGear, Spells, Proficiencies }

internal sealed record LegacyTradeJournalChange(
    LegacyTradeJournalFileKind Kind, long FileId, byte[]? Before, byte[] After);

// One pending record per configured Users root. Live callers must retain the existing
// vendor gate -> snapshot-file locks order; startup recovery runs before live owners.
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

    // Returning true deliberately leaves the durable record in place, without rollback.
    // Count zero is after publication; counts one through four follow target replacement.
    internal static bool TryCommit(string usersRoot, string itemsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes,
        Func<int, bool>? interruptAfterReplacement, out string reason) =>
        TryCommitVersion(usersRoot, itemsRoot, changes, version: 1, interruptAfterReplacement, out reason);

    private static bool TryCommitVersion(string usersRoot, string itemsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, int version,
        Func<int, bool>? interruptAfterReplacement, out string reason)
    {
        reason = string.Empty;
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
        JournalTarget[] targets, Func<int, bool>? interrupt, out string reason)
    {
        reason = string.Empty;
        var published = false;
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
            LegacyMailboxPackageMutationFence.RequireJournalMutation(targets.Select(target => target.Path).ToArray(), record.Changes);
            if (!IsOrdinaryAdmissionVersion(record.Version)) Directory.CreateDirectory(roots.Items);
            Directory.CreateDirectory(roots.Journal);
            EnsureSafePath(roots.Journal);
            WriteFlushed(preparing, Serialize(record));
            File.Move(preparing, pending); // No overwrite: only one transaction can publish.
            published = true;
            if (interrupt?.Invoke(0) == true)
            {
                reason = RecoveryRequired + ":interrupted";
                return false;
            }
            PrepareAdmissionDirectories(roots, record, targets);
            foreach (var target in targets)
                if (!IsOrdinaryAdmissionVersion(record.Version) || !Matches(ReadTarget(target.Path), target.Change.After))
                    Stage(target, target.Change.After, record.TransactionId);
            for (var index = 0; index < targets.Length; index++)
            {
                if (!IsOrdinaryAdmissionVersion(record.Version) || !Matches(ReadTarget(targets[index].Path), targets[index].Change.After))
                    ReplaceStaged(targets[index], record.TransactionId);
                if (interrupt?.Invoke(index + 1) == true)
                {
                    reason = RecoveryRequired + ":interrupted";
                    return false;
                }
            }
            EnsureAllAfter(targets);
            EnsureAdmissionInputs(roots, record);
            EnsureAdmissionStagesCleared(record, targets);
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
