namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask<LegacyOperatorGearMailResult> NotifyOperatorGearMailAsync(
        LegacyOwnedAdmissionRuntime runtime, OperatorMailPlan plan, CancellationToken cancellationToken)
    {
        var request = plan.Request.Request;
        try
        {
            foreach (var frame in plan.Frames)
            {
                if (cancellationToken.IsCancellationRequested || plan.Final is not { } final ||
                    !OperatorMailDeliveryCurrent(runtime, plan, final))
                    return OperatorMailResult(request.OperationId, LegacyOperatorGearMailStatus.CommittedNotificationStopped,
                        "operatorMailCommitted:originalDeliveryOwnerUnavailable", plan.MailIds, plan.ItemUids);
                await plan.Owner.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
            if (plan.Final is not { } last || !OperatorMailDeliveryCurrent(runtime, plan, last))
                return OperatorMailResult(request.OperationId, LegacyOperatorGearMailStatus.CommittedNotificationStopped,
                    "operatorMailCommitted:ownerChangedAfterSend", plan.MailIds, plan.ItemUids);
            return OperatorMailResult(request.OperationId, LegacyOperatorGearMailStatus.CommittedNotificationCompleted,
                "operatorMailCommitted:notificationCompleted", plan.MailIds, plan.ItemUids);
        }
        catch (Exception error)
        {
            // Output failure cannot compensate or rematerialize a durable grant.
            return OperatorMailResult(request.OperationId, LegacyOperatorGearMailStatus.CommittedNotificationStopped,
                "operatorMailCommitted:notification:" + error.GetType().Name, plan.MailIds, plan.ItemUids);
        }
    }

    private bool OperatorMailDeliveryCurrent(LegacyOwnedAdmissionRuntime runtime, OperatorMailPlan plan, WorldSession final)
    {
        // Read the actual trade dictionary under its owner, never across SendAsync.
        lock (_tradeStates.SyncRoot)
            return plan.AfterGraph.IsCurrent(plan.Candidate.PlayerItems) && OperatorMailOwnerCurrent(runtime, plan.Owner, final);
    }
}
