namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleOwnedRepairAsync(
        WorldPacketHandlerContext context, MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        if (context.TradeOperations is not { } operations || context.Trades is not { } trades ||
            context.Sessions is not { } sessions)
        {
            await LogItemRepairRefusalAsync(context, "itemRepairOwnerUnavailable").ConfigureAwait(false);
            return;
        }
        ItemRepairOwner? owner = null;
        ItemRepairPlan? prepared = null;
        ItemRepairPlan? committed = null;
        WorldSession? final = null;
        Exception? writerFault = null;
        Exception? fencingFault = null;
        var durable = false;
        var uncertain = false;
        var reason = "itemRepairOwnerUnavailable";
        try
        {
            using (await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false))
            lock (trades.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sessions.TryGet(context.Connection, out var current) && current is not null)
                {
                    // This closed Store-issued lease proves the actual owning
                    // Frame and complete bound graph before callback-bearing
                    // PIN/catalog/list reads. It is not a persistence grant.
                    using (var preparation = sessions.PrepareItemRepair(context, current))
                    lock (LegacySecondaryPasswordRuntime.SyncRoot)
                    {
                        if (TryCaptureItemRepairOwner(context, preparation, out var captured))
                        {
                            owner = captured;
                            if (TryPrepareItemRepair(context, captured, packet.Id, out var plan, out reason))
                            {
                                plan.Inputs.Arm();
                                preparation.RequireCurrent();
                                prepared = plan;
                            }
                        }
                    }
                    // Store/actor/package preparation scopes have ended. The
                    // actual Store must reacquire them in its normal order.
                    if (owner is { } capturedOwner && prepared is { } repair)
                    {
                        bool Writer()
                        {
                            lock (LegacySecondaryPasswordRuntime.SyncRoot)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                reason = "itemRepairCurrentInputChanged";
                                if (!ItemRepairOwnerIsCurrent(context, capturedOwner, capturedOwner.Expected) ||
                                    !ItemRepairCatalogIsCurrent(context, repair.Catalog) ||
                                    !repair.After.IsCurrent(repair.Updated.PlayerItems) ||
                                    !repair.Inputs.ConsumeBefore())
                                    return false;
                                var outcome = repair.Write.TryCommit(out reason);
                                if (outcome == LegacyItemRepairOutcome.Rejected)
                                    return false;
                                if (outcome == LegacyItemRepairOutcome.RecoveryRequired)
                                {
                                    // This can be a changed prewrite baseline,
                                    // or real WAL/postimage uncertainty. Preserve
                                    // the exact writer reason; neither proves
                                    // that this particular repair committed.
                                    uncertain = true;
                                    FenceUncertainRepair(capturedOwner, reason, ref fencingFault);
                                    throw new LegacyCommittedSpellPublicationException();
                                }
                                durable = true;
                                try
                                {
                                    if (!repair.Inputs.ConsumeAfter() ||
                                        !repair.After.IsCurrent(repair.Updated.PlayerItems))
                                        throw new InvalidOperationException("itemRepairDurableInputChanged");
                                }
                                catch (Exception error)
                                {
                                    writerFault = error;
                                    uncertain = true;
                                    reason = "itemRepairRecoveryRequired:durablePublicationUnproven";
                                    FenceUncertainRepair(capturedOwner, reason, ref fencingFault);
                                    throw new LegacyCommittedSpellPublicationException();
                                }
                                return true;
                            }
                        }
                        if (sessions.TryCommitSession(context.Connection, capturedOwner.Expected,
                            repair.Updated, Writer, out final))
                        {
                            if (final is null)
                                throw new LegacyCommittedSpellPublicationException();
                            committed = repair;
                        }
                    }
                }
            }
        }
        catch (WorldSessionPublicationException published)
        {
            if (owner is not null && published.BelongsTo(sessions, context.Connection))
                try { owner.Recovery.Block("itemRepairRecoveryRequired:postPublication"); }
                catch (Exception secondary) { published.RecordFencingFailure(secondary); }
            throw;
        }
        catch (LegacyCommittedSpellPublicationException error)
        {
            // Store uses this existing typed path to retire the actual holder.
            // Its historical type name alone is not proof of durable repair.
            if (owner is not null)
                FenceUncertainRepair(owner, uncertain ? reason :
                    "itemRepairRecoveryRequired:durablePublicationUnproven", ref fencingFault);
            if (writerFault is not null || fencingFault is not null)
            {
                var failures = new List<Exception> { error };
                if (writerFault is not null)
                    failures.Add(writerFault);
                if (fencingFault is not null)
                    failures.Add(fencingFault);
                throw new AggregateException("Item repair publication requires recovery: " + reason, failures);
            }
            throw;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        {
            if (durable || uncertain)
            {
                if (owner is not null)
                    FenceUncertainRepair(owner, "itemRepairRecoveryRequired:postWriterFault", ref fencingFault);
                if (fencingFault is not null)
                    throw new AggregateException("Item repair and recovery fencing failed.", error, fencingFault);
                throw;
            }
            reason = "itemRepairPreparation:" + error.GetType().Name;
        }
        if (committed is null || owner is null || final is null)
        {
            await LogItemRepairRefusalAsync(context, reason).ConfigureAwait(false);
            return;
        }
        await DeliverItemRepairAsync(context, owner, committed, final, cancellationToken).ConfigureAwait(false);
    }

    private static void FenceUncertainRepair(ItemRepairOwner owner, string reason, ref Exception? failure)
    {
        try { owner.Recovery.Block(reason); }
        catch (Exception error)
        {
            failure = failure is null ? error :
                new AggregateException("Multiple item repair recovery fences failed.", failure, error);
        }
    }

    private static Task LogItemRepairRefusalAsync(WorldPacketHandlerContext context, string reason) =>
        context.Log.WriteLineAsync($"WorldServer skipped MsgItemUsage repair {FormatSession(context)}; reason={reason}.");
}
