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
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            if (!TryCaptureRecovery(usersRoot, itemsRoot, questsRoot, mailboxRoot, combatGearRoot, admissionRoots,
                    out var capture, out reason)) return false;
            return capture is null || RecoverCaptured(capture, out recovered, out reason);
        }
        catch (Exception ex) when (IsJournalException(ex))
        {
            reason = RecoveryRequired + ":invalidOrUnreadable:" + ex.GetType().Name;
            return false;
        }
    }

    private static bool RecoverLocked(JournalRoots roots, JournalRecord record,
        JournalTarget[] targets, string pending, out string reason, OwnedStartupItemRecovery? ownedStartup = null)
    {
        reason = string.Empty;
        var validated = false;
        try
        {
            ownedStartup?.RequireCurrent();
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
            if (ownedStartup is null)
            {
                if (!IsOrdinaryAdmissionVersion(record.Version) && record.Version is not (8 or 9 or 11 or 12 or 13 or 14 or 15 or 16 or 17)) Directory.CreateDirectory(roots.Items);
                if (record.Version is 9 or 12 or 13 or 14 or 15 or 16 or 17) PrepareCharacterItemsSpellsDirectories(roots, record, targets);
                else if (record.Version == 8) PrepareCharacterSpellsDirectories(roots, record, targets);
                else PrepareAdmissionDirectories(roots, record, targets);
            }
            else ownedStartup.PrepareDirectories();
            foreach (var target in targets)
            {
                ownedStartup?.RequireCurrent();
                if (Matches(ReadTarget(target.Path), target.Change.After)) continue;
                var staged = StagePath(target, record.TransactionId);
                if (!TryDelete(staged)) throw new IOException("Cannot clear previous staging file.");
                ownedStartup?.RequireCurrent();
                Stage(target, target.Change.After, record.TransactionId);
            }
            foreach (var target in targets)
            {
                ownedStartup?.RequireCurrent();
                if (Matches(ReadTarget(target.Path), target.Change.After)) continue;
                ownedStartup?.RequireCurrent();
                ReplaceStaged(target, record.TransactionId);
            }
            EnsureAllAfter(targets);
            EnsureAdmissionInputs(roots, record);
            if (ownedStartup is null) EnsureAdmissionStagesCleared(record, targets);
            else ownedStartup.ClearStages();
            ownedStartup?.RequireCurrent();
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
            if (validated && ownedStartup is null)
                foreach (var target in targets) TryDelete(StagePath(target, record.TransactionId));
        }
    }
}
