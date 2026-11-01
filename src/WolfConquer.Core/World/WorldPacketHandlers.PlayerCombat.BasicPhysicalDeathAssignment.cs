namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        public async ValueTask<bool> AssignNormalDeathHitpointsAsync()
        {
            if (Context.BasicPhysicalReceiveDecision is not { } decision) return await AssignAsync(0).ConfigureAwait(false);
            if (!Refresh()) return false;
            Adopt(await AssignLegacyPlayerHitpointsAsync(Context, Session, Request.Scope, 0, CancellationToken, decision)
                .ConfigureAwait(false));
            if (!Current.Completed) return false;
            Receipt = Receipt with
            {
                Phases = Receipt.Phases | LegacyEquipmentQueryPublishedPhases.Hitpoints,
                HitpointAssignments = Receipt.HitpointAssignments + 1,
                HitpointPairs = Receipt.HitpointPairs + 1,
            };
            return true;
        }
    }
}
