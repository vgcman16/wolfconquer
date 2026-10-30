namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryRefreshCommittedEquipmentMove(WorldPacketHandlerContext context, EquipmentMoveOwner owner,
        WolfConquer.Core.Data.PlayerItemSnapshot committedItems, ref WorldSession session)
    {
        if (context.Sessions is not { } sessions ||
            !LegacyNativeClientPublication.TryCurrent(sessions, context.Connection, owner.Expected.Character.Uid,
                owner.Identity, out var current) ||
            !ReferenceEquals(current.PlayerItems, committedItems) ||
            !EquipmentMoveOwnerIsCurrent(context, owner, current)) return false;
        session = current; return true;
    }

    private static async ValueTask DeliverEquipmentMoveAsync(WorldPacketHandlerContext context, EquipmentMoveOwner owner,
        EquipmentMovePlan plan, WorldSession session, CancellationToken cancellationToken)
    {
        var committedItems = session.PlayerItems;
        try
        {
            foreach (var frame in plan.Frames)
            {
                if (!TryRefreshCommittedEquipmentMove(context, owner, committedItems, ref session)) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!TryRefreshCommittedEquipmentMove(context, owner, committedItems, ref session)) return;
            }
            context = context with { Session = session };
            if (plan.QueryUnconditionally || session.AlternateEquipmentActive)
            {
                var query = await ExecuteLegacyEquipmentQueryAsync(context, session,
                    new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.EquipmentMutation,
                        new LegacyEquipmentQueryPublicationScope(owner.Expected.Character.Uid, committedItems)
                        { OwnerNativeClientIdentity = owner.Identity }), cancellationToken).ConfigureAwait(false);
                if (!query.Completed) return;
                session = query.Session!;
            }
            else
            {
                if (!TryRefreshCommittedEquipmentMove(context, owner, committedItems, ref session)) return;
                await context.Connection.SendAsync(new LegacyShowEquipmentPacket(WParam: 0, Alternante: 0,
                    Action: LegacyShowEquipmentPacket.AllowAlternanteAction, EquipmentSlots: default).ToFrameBytes(),
                    cancellationToken).ConfigureAwait(false);
            }
            if (!TryRefreshCommittedEquipmentMove(context, owner, committedItems, ref session)) return;
            // No late best-effort item rewrite. Membership was durably committed
            // before the first response; later query sidecars keep their owners.
            await context.Log.WriteLineAsync($"WorldServer handled MsgItemUsage {plan.Operation} " +
                $"{FormatSession(context with { Session = session })}; item move persistedBeforeReply=True.").ConfigureAwait(false);
        }
        catch (WorldSessionPublicationException published)
        {
            if (published.BelongsTo(context.Sessions, context.Connection))
                try { owner.Recovery.Block("equipmentMoveRecoveryRequired:queryPostPublication"); }
                catch (Exception secondary) { published.RecordFencingFailure(secondary); }
            throw;
        }
        // A transport fault is a committed notification failure. Propagate it;
        // never refund, rerun the writer or claim that the move was unchanged.
    }
}
