using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private LegacyOperatorGearMailResult CommitOperatorMail(LegacyOwnedAdmissionRuntime runtime, OperatorMailPlan plan,
        CancellationToken cancellationToken, out OperatorMailPlan? delivery)
    {
        delivery = null;
        var durable = false;
        var uncertain = false;
        var reason = "operatorMailCommitOwnerChanged:reservationBurned";
        try
        {
            var committed = _sessions.TryCommitSession(plan.Owner.Connection, plan.Owner.Expected, plan.Candidate,
                () => SnapshotFileLockCoordinator.ExecuteLocked(plan.Owner.Paths, () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!OperatorMailPlanCurrent(runtime, plan) ||
                        !OperatorMailFilesMatch(plan.Owner, plan.Before.Character.Bytes, plan.Before.Items, plan.Before.Mailbox) ||
                        !OperatorMailOwnerCurrent(runtime, plan.Owner, plan.Owner.Expected)) return false;
                    uncertain = true; // Only a returned, proved outcome can clear this journal-attempt uncertainty.
                    var saved = LegacyTradeJournal.TryCommitMailboxOwner(plan.Owner.Routes.UsersRoot,
                        plan.Owner.Routes.ItemsRoot, plan.Owner.Routes.MailboxRoot,
                    [
                        new(LegacyTradeJournalFileKind.Character, plan.Owner.Expected.AccountId,
                            plan.Before.Character.Bytes, plan.Before.Character.Bytes),
                        new(LegacyTradeJournalFileKind.Items, plan.Owner.Expected.Character.Uid, plan.Before.Items, plan.ItemsAfter),
                        new(LegacyTradeJournalFileKind.Mailbox, plan.Owner.Expected.Character.Uid, plan.Before.Mailbox, plan.MailAfter),
                    ], out reason);
                    if (!saved)
                    {
                        uncertain = reason.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal) ||
                            LegacyTradeJournal.HasPending(plan.Owner.Routes.UsersRoot) ||
                            !OperatorMailFilesMatch(plan.Owner, plan.Before.Character.Bytes, plan.Before.Items, plan.Before.Mailbox);
                        if (uncertain) FenceOperatorMail("operatorMailRecoveryRequired:" + reason);
                        return false;
                    }
                    durable = true;
                    uncertain = false;
                    if (!OperatorMailFilesMatch(plan.Owner, plan.Before.Character.Bytes, plan.ItemsAfter, plan.MailAfter) ||
                        !OperatorMailPlanCurrent(runtime, plan))
                        throw new LegacyCommittedSpellPublicationException();
                    _mailboxes!.PublishOperatorDelivery(plan.Before.Mail, plan.MailProjection);
                    plan.Owner.Binding.Client.RawEquipmentObservation?.InvalidateRawLoadObservation("operatorGearMailCommitted");
                    // The actual Store then consumes its already armed final
                    // native proofs and publishes the real final record, not Candidate.
                    return true;
                }), out var final);
            if (!committed || final is null)
                return OperatorMailResult(plan.Request.Request.OperationId,
                    uncertain ? LegacyOperatorGearMailStatus.RecoveryRequired : LegacyOperatorGearMailStatus.Rejected,
                    (uncertain ? "operatorMailRecoveryRequired:" : "operatorMailWriteRejected:reservationBurned:") + reason);
            plan.Final = final;
            delivery = plan;
            return OperatorMailResult(plan.Request.Request.OperationId, LegacyOperatorGearMailStatus.CommittedNotificationStopped,
                "operatorMailCommitted:notificationPending", plan.MailIds, plan.ItemUids);
        }
        catch (Exception error)
        {
            // A true writer or uncertain journal is never an unchanged refusal.
            if (durable || uncertain || error is LegacyCommittedSpellPublicationException ||
                LegacyTradeJournal.HasPending(plan.Owner.Routes.UsersRoot))
            {
                FenceOperatorMail("operatorMailRecoveryRequired:" + error.GetType().Name);
                return OperatorMailResult(plan.Request.Request.OperationId, LegacyOperatorGearMailStatus.RecoveryRequired,
                    durable ? "operatorMailCommittedPublicationUnproven:" + error.GetType().Name :
                        "operatorMailPersistenceOutcomeUnproven:" + error.GetType().Name);
            }
            return OperatorMailResult(plan.Request.Request.OperationId, LegacyOperatorGearMailStatus.Rejected,
                "operatorMailCommitPreparation:" + error.GetType().Name + ":reservationBurned");
        }
    }

    private bool OperatorMailPlanCurrent(LegacyOwnedAdmissionRuntime runtime, OperatorMailPlan plan) =>
        plan.Request.Catalogs.All(catalog => catalog.IsCurrent()) &&
        plan.Before.Graph.IsCurrent(plan.Owner.Expected.PlayerItems) && plan.AfterGraph.IsCurrent(plan.Candidate.PlayerItems) &&
        plan.Before.Mail.IsCurrent() && OperatorMailOwnerCurrent(runtime, plan.Owner, plan.Owner.Expected);

    private void FenceOperatorMail(string reason)
    {
        _tradeRecovery.Block(reason);
        _mailboxes!.BlockClaims(reason);
    }
}
