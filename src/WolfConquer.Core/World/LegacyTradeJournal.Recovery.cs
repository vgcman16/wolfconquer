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
        out int recovered, out string reason) =>
        TryRecover(usersRoot, itemsRoot, questsRoot, mailboxRoot, combatGearRoot: null, out recovered, out reason);

    internal static bool TryRecover(string usersRoot, string itemsRoot, string? questsRoot, string? mailboxRoot,
        string? combatGearRoot, out int recovered, out string reason)
        => TryRecover(usersRoot, itemsRoot, questsRoot, mailboxRoot, combatGearRoot, null, out recovered, out reason);

    internal static bool TryRecover(string usersRoot, string itemsRoot, string? questsRoot, string? mailboxRoot,
        string? combatGearRoot, LegacyOrdinaryAdmissionRoots? admissionRoots, out int recovered, out string reason)
    {
        recovered = 0;
        reason = string.Empty;
        try
        {
            // A fresh configuration without a journal needs no snapshot directories yet.
            if (!HasPending(usersRoot)) return true;
            var journal = Path.Combine(Path.GetFullPath(usersRoot), JournalDirectoryName);
            EnsureSafePath(journal);
            var pending = Path.Combine(journal, PendingName);
            var children = Directory.GetFileSystemEntries(journal);
            if (children.Length != 1 || !StringComparer.Ordinal.Equals(children[0], pending))
            {
                reason = RecoveryRequired + ":unexpectedJournalEntry";
                return false;
            }
            var record = ReadRecord(pending);
            JournalRoots roots;
            if (IsOrdinaryAdmissionVersion(record.Version))
                roots = GetAdmissionRoots(admissionRoots ?? throw new ArgumentException("Explicit ordinary-admission roots are required."));
            else if (record.Version == 8)
            {
                if (admissionRoots?.Spells is not { } configuredSpells)
                {
                    reason = RecoveryRequired + ":characterSpellsRootMissing";
                    return false;
                }
                roots = GetCharacterSpellsRoots(admissionRoots.Users, configuredSpells);
                if (!SameQuestRoot(usersRoot, roots.Users))
                {
                    reason = RecoveryRequired + ":characterSpellsUsersMismatch";
                    return false;
                }
            }
            else roots = GetRoots(usersRoot, itemsRoot);
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
            if (record.Version == 5)
            {
                if (string.IsNullOrWhiteSpace(mailboxRoot) || string.IsNullOrWhiteSpace(combatGearRoot))
                {
                    reason = RecoveryRequired + ":mailboxOrCombatGearRootMissing";
                    return false;
                }
                roots = GetMailboxGearRoots(usersRoot, itemsRoot, mailboxRoot, combatGearRoot);
            }
            var targets = GetTargets(roots, record);
            var lockedReason = string.Empty;
            var result = SnapshotFileLockCoordinator.ExecuteLocked(AdmissionLockPaths(roots, record, targets),
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
            EnsureAdmissionInputs(roots, record);
            if (record.Version == 6)
            {
                // The unpublished prototype could tune away saved progress.
                // It has no ordinary-login migration authority: do not stage,
                // replace, roll forward or discard any before/mixed evidence.
                EnsureAllAfter(targets);
                EnsureAdmissionStagesCleared(record, targets);
                DeleteRecord(pending);
                return true;
            }
            validated = true;
            if (!IsOrdinaryAdmissionVersion(record.Version) && record.Version != 8) Directory.CreateDirectory(roots.Items);
            if (record.Version == 8) PrepareCharacterSpellsDirectories(roots, record, targets);
            else PrepareAdmissionDirectories(roots, record, targets);
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
            EnsureAdmissionInputs(roots, record);
            EnsureAdmissionStagesCleared(record, targets);
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
