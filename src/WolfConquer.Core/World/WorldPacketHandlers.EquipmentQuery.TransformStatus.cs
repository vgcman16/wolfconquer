namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<LegacyEquipmentQueryPublicationResult> UpdateLegacyClientTransformStatusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyEquipmentQueryPublicationScope scope,
        uint capturedTransformMaximum,
        CancellationToken cancellationToken)
    {
        // ClientTransform.UpdateStatus: setter uses PRIOR current Status.Max.
        var current = await AssignLegacyPlayerHitpointsAsync(
                context, session, scope, unchecked((int)capturedTransformMaximum), cancellationToken)
            .ConfigureAwait(false);
        if (!current.Completed) return current;
        current = CommitLegacyEquipmentQueryMutation(context, current.Session!, scope,
            owner => owner with
            {
                EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(scope.OwnerUid, capturedTransformMaximum),
            });
        if (!current.Completed) return current;
        return await SendLegacyCurrentStatusHitpointPairAsync(
                context, current.Session!, scope, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed partial class LegacyEquipmentQueryExecution
    {
        public async ValueTask<bool> UpdateTransformAsync(uint capturedMaximum)
        {
            if (!Refresh()) return false;
            Adopt(await UpdateLegacyClientTransformStatusAsync(
                    Context, Session, Request.Scope, capturedMaximum, CancellationToken).ConfigureAwait(false));
            if (!Current.Completed) return false;
            Receipt = Receipt with
            {
                Phases = Receipt.Phases | LegacyEquipmentQueryPublishedPhases.Hitpoints,
                HitpointAssignments = Receipt.HitpointAssignments + 1,
                HitpointPairs = Receipt.HitpointPairs + 2,
            };
            return true;
        }
    }
}
