namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleOwnedHealthConsumableAsync(WorldPacketHandlerContext context,
        WorldSession supplied, uint uid, CancellationToken cancellationToken)
    {
        HealthConsumableOwner? owner = null; HealthConsumablePlan? committed = null; WorldSession? final = null;
        var reason = "healthConsumableOwnerUnavailable"; var durable = false;
        if (context.Trades is not { } trades || context.Sessions is not { } sessions)
        { await LogHealthConsumableAsync(context, reason).ConfigureAwait(false); return; }
        try
        {
            // The actual Frame/equipment dispatcher already owns its packet
            // operation lease. Never nest a Shared lease or upgrade it here.
            lock (trades.SyncRoot)
            lock (LegacySecondaryPasswordRuntime.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryCaptureHealthConsumableOwner(context, supplied, uid, out var captured))
                {
                    owner = captured;
                    if (TryPrepareHealthConsumable(context, captured, out var plan, out reason))
                    {
                        plan.Inputs.Arm();
                        bool Writer()
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!HealthConsumableOwnerIsCurrent(context, captured, captured.Expected) ||
                                !HealthConsumableCatalogIsCurrent(context, plan.Catalog) ||
                                !plan.After.IsCurrent(plan.Updated.PlayerItems) || !plan.Inputs.ConsumeBefore())
                            { reason = "healthConsumableCurrentInputsChanged"; return false; }
                            var result = plan.Write.TryCommit(out reason);
                            if (result == LegacyHealthConsumableOutcome.Rejected) return false;
                            if (result == LegacyHealthConsumableOutcome.RecoveryRequired)
                            {
                                captured.Recovery.Block("healthConsumableRecoveryRequired:" + reason);
                                throw new LegacyCommittedSpellPublicationException();
                            }
                            durable = true;
                            if (!plan.Inputs.ConsumeAfter() || !plan.After.IsCurrent(plan.Updated.PlayerItems))
                            {
                                captured.Recovery.Block("healthConsumableRecoveryRequired:durableInputsChanged");
                                throw new LegacyCommittedSpellPublicationException();
                            }
                            return true;
                        }
                        if (sessions.TryCommitSession(context.Connection, captured.Expected, plan.Updated, Writer, out final))
                        {
                            if (final is null) throw new LegacyCommittedSpellPublicationException();
                            committed = plan;
                        }
                    }
                }
            }
        }
        catch (WorldSessionPublicationException published)
        {
            if (owner is not null && published.BelongsTo(sessions, context.Connection))
                try { owner.Recovery.Block("healthConsumableRecoveryRequired:postPublication"); }
                catch (Exception secondary) { published.RecordFencingFailure(secondary); }
            throw;
        }
        catch (LegacyCommittedSpellPublicationException)
        {
            owner?.Recovery.Block("healthConsumableRecoveryRequired:durablePublicationUnproven");
            await LogHealthConsumableAsync(context, "recoveryRequired; no automatic retry or refund").ConfigureAwait(false);
            return;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        {
            if (durable)
            {
                owner!.Recovery.Block("healthConsumableRecoveryRequired:postCommitFault"); owner.Binding.Retire();
                throw;
            }
            reason = "healthConsumablePreparation:" + error.GetType().Name;
        }
        if (owner is null || committed is null || final is null)
        { await LogHealthConsumableAsync(context, reason).ConfigureAwait(false); return; }
        try
        {
            foreach (var frame in committed.Frames)
            {
                if (!Current()) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!Current()) return;
            }
            await LogHealthConsumableAsync(context with { Session = final }, "committedBeforeReply").ConfigureAwait(false);
        }
        catch
        {
            // The journal already succeeded. A send fault is a committed
            // notification failure, never an unchanged rejection or refund.
            owner.Recovery.Block("healthConsumableRecoveryRequired:committedNotificationFault"); owner.Binding.Retire();
            throw;
        }
        bool Current()
        {
            lock (trades.SyncRoot)
            lock (LegacySecondaryPasswordRuntime.SyncRoot)
                return HealthConsumableOwnerIsCurrent(context, owner, final);
        }
    }

    private static Task LogHealthConsumableAsync(WorldPacketHandlerContext context, string reason) =>
        context.Log.WriteLineAsync($"WorldServer health consumable {FormatSession(context)} reason={reason}.");
}
