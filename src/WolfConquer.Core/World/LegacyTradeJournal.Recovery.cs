using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryRecover(string usersRoot, string itemsRoot, out int recovered, out string reason) =>
        TryRecover(usersRoot, itemsRoot, questsRoot: null, out recovered, out reason);

    internal static bool TryRecover(string usersRoot, string itemsRoot, string? questsRoot,
        out int recovered, out string reason) =>
        TryRecover(usersRoot, itemsRoot, questsRoot, mailboxRoot: null, out recovered, out reason);

    internal static bool TryRecover(string usersRoot, string itemsRoot, string? questsRoot, string? mailboxRoot,
        out int recovered, out string reason)
    {
        recovered = 0;
        reason = string.Empty;
        try
        {
            // A fresh configuration without a journal needs no snapshot directories yet.
            if (!HasPending(usersRoot)) return true;
            var roots = GetRoots(usersRoot, itemsRoot);
            var pending = Path.Combine(roots.Journal, PendingName);
            var children = Directory.GetFileSystemEntries(roots.Journal);
            if (children.Length != 1 || !StringComparer.Ordinal.Equals(children[0], pending))
            {
                reason = RecoveryRequired + ":unexpectedJournalEntry";
                return false;
            }
            var record = ReadRecord(pending);
            if (record.Version == 3)
            {
                if (string.IsNullOrWhiteSpace(questsRoot))
                {
                    reason = RecoveryRequired + ":questsRootMissing";
                    return false;
                }
                roots = GetQuestRoots(usersRoot, itemsRoot, questsRoot);
            }
            if (record.Version == 4)
            {
                if (string.IsNullOrWhiteSpace(mailboxRoot))
                {
                    reason = RecoveryRequired + ":mailboxRootMissing";
                    return false;
                }
                roots = GetMailboxRoots(usersRoot, itemsRoot, mailboxRoot);
            }
            var targets = GetTargets(roots, record);
            var lockedReason = string.Empty;
            var result = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(),
                () => RecoverLocked(roots, record, targets, pending, out lockedReason));
            reason = lockedReason;
            recovered = result ? 1 : 0;
            return result;
        }
        catch (Exception ex) when (IsJournalException(ex))
        {
            reason = RecoveryRequired + ":invalidOrUnreadable:" + ex.GetType().Name;
            return false;
        }
    }

    private static bool RecoverLocked(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets, string pending, out string reason)
    {
        reason = string.Empty;
        var validated = false;
        try
        {
            // No target changes until every current image has passed validation.
            EnsureTargetsMatch(targets, allowAfter: true);
            validated = true;
            Directory.CreateDirectory(roots.Items);
            foreach (var target in targets)
            {
                if (Matches(ReadTarget(target.Path), target.Change.After)) continue;
                var staged = StagePath(target, record.TransactionId);
                if (!TryDelete(staged)) throw new IOException("Cannot clear previous staging file.");
                Stage(target, target.Change.After, record.TransactionId);
            }
            foreach (var target in targets)
            {
                if (Matches(ReadTarget(target.Path), target.Change.After)) continue;
                ReplaceStaged(target, record.TransactionId);
            }
            EnsureAllAfter(targets);
            DeleteRecord(pending);
            return true;
        }
        catch (Exception ex) when (IsJournalException(ex))
        {
            // Keep all evidence; a later recovery can retry the remaining prefix.
            reason = RecoveryRequired + ":rollForwardFailed:" + ex.GetType().Name;
            return false;
        }
        finally
        {
            if (validated)
                foreach (var target in targets) TryDelete(StagePath(target, record.TransactionId));
        }
    }
}
