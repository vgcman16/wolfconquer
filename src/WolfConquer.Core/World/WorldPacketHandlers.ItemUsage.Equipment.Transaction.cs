namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask CommitAndDeliverEquipmentMoveAsync(WorldPacketHandlerContext context,
        MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        if (context.TradeOperations is not { } operations || context.Trades is not { } trades || context.Sessions is not { } sessions)
        { await LogEquipmentMoveRefusalAsync(context, "equipmentMoveOwnerUnavailable").ConfigureAwait(false); return; }
        EquipmentMoveOwner? owner = null; EquipmentMovePlan? committed = null; WorldSession? final = null;
        var reason = "equipmentMoveOwnerUnavailable"; var durable = false;
        try
        {
            using (await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false))
            lock (trades.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryCaptureEquipmentMoveOwner(context, out var captured, out reason))
                {
                    owner = captured;
                    if (TryPrepareEquipmentMove(context, captured, packet, out var plan, out reason))
                    {
                        // All potentially caller-owned enumerator acquisition is
                        // before the real Store/native preparation and journal.
                        plan.Inputs.Arm();
                        var failure = "equipmentMoveCurrentOwnerChanged";
                        bool Writer()
                        {
                            lock (LegacySecondaryPasswordRuntime.SyncRoot)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (!EquipmentMoveOwnerIsCurrent(context, captured, captured.Expected) ||
                                    !EquipmentMoveAdmissionIsCurrent(context, captured) ||
                                    !EquipmentMoveCatalogIsCurrent(context, plan.Catalog) ||
                                    !plan.After.IsCurrent(plan.Updated.PlayerItems) || !plan.Inputs.ConsumeBefore()) return false;
                                if (!LegacyEquipmentMovePersistence.TryPersist(captured.Expected, plan.Inputs.Stable,
                                    plan.Updated, captured.ItemsRoot, plan.Inputs.Before, plan.After, out failure))
                                {
                                    if (failure.StartsWith(LegacyEquipmentMovePersistence.RecoveryRequired, StringComparison.Ordinal))
                                        captured.Recovery.Block(failure);
                                    return false;
                                }
                                durable = true;
                                // The real writer succeeded. A later source-map
                                // conflict is committed uncertainty, never refusal.
                                if (!plan.Inputs.ConsumeAfter() || !plan.After.IsCurrent(plan.Updated.PlayerItems))
                                {
                                    captured.Recovery.Block("equipmentMoveRecoveryRequired:durableInputChanged");
                                    throw new LegacyCommittedSpellPublicationException();
                                }
                                return true;
                            }
                        }
                        if (sessions.TryCommitSession(context.Connection, captured.Expected, plan.Updated, Writer, out final))
                        {
                            if (final is null) throw new LegacyCommittedSpellPublicationException();
                            committed = plan;
                        }
                        else reason = failure;
                    }
                }
            }
        }
        catch (WorldSessionPublicationException published)
        {
            if (owner is not null && published.BelongsTo(sessions, context.Connection))
                try { owner.Recovery.Block("equipmentMoveRecoveryRequired:postPublication"); }
                catch (Exception secondary) { published.RecordFencingFailure(secondary); }
            throw;
        }
        catch (LegacyCommittedSpellPublicationException error)
        {
            try { owner?.Recovery.Block("equipmentMoveRecoveryRequired:durablePublicationUnproven"); }
            catch (Exception secondary)
            { throw new AggregateException("Equipment publication and recovery fence failed.", error, secondary); }
            throw;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        {
            if (durable)
            {
                try { owner!.Recovery.Block("equipmentMoveRecoveryRequired:postCommitFault"); }
                catch (Exception secondary)
                { throw new AggregateException("Equipment commit and recovery fence failed.", error, secondary); }
                throw;
            }
            reason = "equipmentMovePreparation:" + error.GetType().Name;
        }
        if (committed is null || owner is null || final is null)
        { await LogEquipmentMoveRefusalAsync(context, reason).ConfigureAwait(false); return; }

        // Existing equipment-query/status writers run under Shared. This is a
        // new lease AFTER Exclusive and all Trades/Store/file gates have ended.
        using (await operations.EnterPacketSharedAsync(cancellationToken).ConfigureAwait(false))
            await DeliverEquipmentMoveAsync(context, owner, committed, final, cancellationToken).ConfigureAwait(false);
    }
}
