using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Called only inside the real Store persisted CAS, after all output preparation.
    private static bool TryPersistLegacyMailboxClaim(LegacyMailboxClaimPlan plan, out string reason)
    {
        reason = MailboxClaimPrefix + "CandidateInvalid";
        if (!TryProjectMailboxClaimCharacter(plan, out var characterAfter) || !MailboxClaimPlanCurrent(plan)) return false;
        var itemsAfter = PlayerItemSnapshotLoader.ToLegacyBytes(
            plan.ItemsAfter.WithOwnedItemsForPersistence(plan.Updated).PlayerItems);
        var before = plan.Baseline;
        var failure = reason;
        var saved = SnapshotFileLockCoordinator.ExecuteLocked(before.Paths.All, () =>
            PersistMailboxClaimLocked(plan, characterAfter, itemsAfter, out failure));
        reason = failure;
        return saved;
    }

    private static bool MailboxClaimPlanCurrent(LegacyMailboxClaimPlan plan) =>
        plan.Baseline.Items.IsCurrent(plan.Baseline.Owner.Expected.PlayerItems) &&
        plan.ItemsAfter.IsCurrent(plan.Updated.PlayerItems) && plan.Baseline.Mail.IsCurrent() &&
        plan.Selected.All(mail => mail.IsLive(plan.Baseline.Mail.Now)) &&
        MailboxClaimAdmissionCurrent(plan.Baseline.Owner);

    private static bool PersistMailboxClaimLocked(LegacyMailboxClaimPlan plan,
        byte[] characterAfter, byte[] itemsAfter, out string reason)
    {
        var before = plan.Baseline;
        var paths = before.Paths;
        reason = MailboxClaimPrefix + "PreparedFileChanged";
        if (LegacyTradeJournal.HasPending(paths.Users))
        { reason = MailboxClaimRecoveryRequired + ":pending"; return false; }
        var mailBefore = before.Mail.CopyImage();
        if (!ProveMailboxClaimFiles(paths, before.Character.Bytes, before.ItemsBefore, mailBefore) ||
            !MailboxClaimPlanCurrent(plan)) return false;
        // Journal copies every prepared image before its pending record is published.
        bool committed;
        string failure;
        try
        {
            committed = LegacyTradeJournal.TryCommitMailboxOwner(paths.Users, paths.ItemsRoot, paths.MailboxRoot,
            [
                new(LegacyTradeJournalFileKind.Character, before.Owner.Expected.AccountId, before.Character.Bytes, characterAfter),
                new(LegacyTradeJournalFileKind.Items, before.Owner.Expected.Character.Uid, before.ItemsBefore, itemsAfter),
                new(LegacyTradeJournalFileKind.Mailbox, before.Owner.Expected.Character.Uid, mailBefore, plan.MailAfter.Image),
            ], out failure);
        }
        catch (Exception error) when (IsMailboxClaimPreparationFailure(error))
        { reason = MailboxClaimRecoveryRequired + ":writeFault:" + error.GetType().Name; return false; }
        try
        {
            if (committed)
            {
                if (ProveMailboxClaimFiles(paths, characterAfter, itemsAfter, plan.MailAfter.Image))
                { reason = string.Empty; return true; }
                reason = MailboxClaimRecoveryRequired + ":postimagesUnproven";
                return false;
            }
            if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal) ||
                LegacyTradeJournal.HasPending(paths.Users))
            { reason = MailboxClaimRecoveryRequired + ":" + failure; return false; }
            if (ProveMailboxClaimFiles(paths, before.Character.Bytes, before.ItemsBefore, mailBefore))
            { reason = MailboxClaimPrefix + "WriteRejected:" + failure; return false; }
            reason = MailboxClaimRecoveryRequired + ":preimagesUnproven:" + failure;
            return false;
        }
        catch (Exception error) when (IsMailboxClaimPreparationFailure(error))
        { reason = MailboxClaimRecoveryRequired + ":postWriteObservation:" + error.GetType().Name; return false; }
    }

    private static bool ProveMailboxClaimFiles(LegacyMailboxClaimPaths paths, byte[] character, byte[] items, byte[] mailbox) =>
        CharacterSnapshotFileBaseline.TryReadBytes(paths.Character, MailboxClaimPrefix, out var c, out _) &&
        c.AsSpan().SequenceEqual(character) &&
        CharacterSnapshotFileBaseline.TryReadBytes(paths.Items, MailboxClaimPrefix, out var i, out _) &&
        i.AsSpan().SequenceEqual(items) &&
        CharacterSnapshotFileBaseline.TryReadBytes(paths.Mailbox, MailboxClaimPrefix, out var m, out _) &&
        m.AsSpan().SequenceEqual(mailbox);
}
