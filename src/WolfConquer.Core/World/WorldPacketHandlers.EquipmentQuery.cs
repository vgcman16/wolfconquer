namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<LegacyEquipmentQueryOutcome> ExecuteLegacyEquipmentQueryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyEquipmentQueryRequest request,
        CancellationToken cancellationToken)
    {
        var execution = new LegacyEquipmentQueryExecution(context, session, request)
        { CancellationToken = cancellationToken };
        if (!execution.Refresh()) return execution.Outcome();

        // ONE admission decision per actual source query, before AppendItems.
        var decision = LegacyEquipmentRebuildAdmission.ApplyCurrent(
            execution.Session, request.Reason, context.LegacyClock ?? LegacySignedMillisecondClock.System);
        execution.Admitted = decision.Admitted;
        if (!execution.Commit(current => current with
            {
                FineRainEquipmentAdmission = decision.Session.FineRainEquipmentAdmission,
                PhysicalStatusOverlay = decision.Admitted
                    ? current.PhysicalStatusOverlay : decision.Session.PhysicalStatusOverlay,
            }))
            return execution.Outcome();
        if (!decision.Admitted)
        {
            await execution.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes(),
                    LegacyEquipmentQueryPublishedPhases.None)
                .ConfigureAwait(false);
            return execution.Outcome();
        }

        if (!execution.CaptureEquipment()) return execution.Outcome();
        if (request.CallItems &&
            !await execution.SendAsync(LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    execution.SourceItems, execution.CapturedAlternate).ToFrameBytes(),
                LegacyEquipmentQueryPublishedPhases.AppendItems).ConfigureAwait(false))
            return execution.Outcome();
        if (!await execution.RunStatsPrefixAsync().ConfigureAwait(false) ||
            !await execution.RunRuneHitpointsAsync().ConfigureAwait(false))
            return execution.Outcome();

        if (!await execution.SendAsync(
                CreateNativeMagicCachedPlayerStatusResponse(execution.Context, execution.Session,
                    execution.Context.Connection, execution.Request.Scope.OwnerNativeClientIdentity).ToFrameBytes(),
                LegacyEquipmentQueryPublishedPhases.Status).ConfigureAwait(false))
            return execution.Outcome();

        if (!execution.Refresh() ||
            !await execution.AssignAsync(Math.Min(unchecked((int)execution.Session.Character.MinHitPoints),
                unchecked((int)execution.Maximum))).ConfigureAwait(false))
            return execution.Outcome();
        if (!execution.Refresh()) return execution.Outcome();
        if (LegacyTransformState.TryGetActiveMaxHitpoints(execution.Session.Character, out var transform))
        {
            if (!await execution.UpdateTransformAsync(transform).ConfigureAwait(false))
                return execution.Outcome();
        }
        else if (!await execution.PairAsync().ConfigureAwait(false))
            return execution.Outcome();
        // QueryEquipment postlude follows OnDequeue's HP work.
        if (execution.Refresh() &&
            await execution.SendAsync(LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(
                    CheckedUInt32(request.Scope.OwnerUid, nameof(request.Scope.OwnerUid)),
                    execution.Session.PlayerItems).ToFrameBytes(),
                LegacyEquipmentQueryPublishedPhases.Perfection).ConfigureAwait(false))
            await execution.RunWindWalkerPostludeAsync().ConfigureAwait(false);
        return execution.Outcome();
    }
}
