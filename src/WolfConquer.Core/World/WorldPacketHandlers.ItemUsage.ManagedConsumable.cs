namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private enum ManagedConsumableOutcome { Rejected, Committed, RecoveryRequired }

    // Closed adapters of the two real resource plans/writers. This is not a
    // caller-supplied permission, saver or arbitrary scalar-mutation callback.
    private sealed record ManagedConsumableCommitPlan(WorldSession Updated, EquipmentMoveInputs Inputs,
        LegacyItemSaveGraph After, HealthConsumableCatalog Catalog, byte[][] Frames,
        LegacyManagedManaConsumableWrite? ManaWrite, LegacyFullRestoreConsumableWrite? FullRestoreWrite)
    {
        internal ManagedConsumableOutcome TryCommit(out string reason)
        {
            if (ManaWrite is { } mana && FullRestoreWrite is null)
                return mana.TryCommit(out reason) switch
                {
                    LegacyManagedManaConsumableOutcome.Rejected => ManagedConsumableOutcome.Rejected,
                    LegacyManagedManaConsumableOutcome.Committed => ManagedConsumableOutcome.Committed,
                    LegacyManagedManaConsumableOutcome.RecoveryRequired => ManagedConsumableOutcome.RecoveryRequired,
                    _ => throw InvalidEquipmentMove()
                };
            if (FullRestoreWrite is { } restore && ManaWrite is null)
                return restore.TryCommit(out reason) switch
                {
                    LegacyFullRestoreConsumableOutcome.Rejected => ManagedConsumableOutcome.Rejected,
                    LegacyFullRestoreConsumableOutcome.Committed => ManagedConsumableOutcome.Committed,
                    LegacyFullRestoreConsumableOutcome.RecoveryRequired => ManagedConsumableOutcome.RecoveryRequired,
                    _ => throw InvalidEquipmentMove()
                };
            throw InvalidEquipmentMove();
        }
    }

    private static bool TryPrepareManagedConsumable(WorldPacketHandlerContext context, ManagedManaConsumableOwner owner,
        LegacyConsumableItemKind kind, out ManagedConsumableCommitPlan plan, out string reason)
    {
        plan = null!; reason = "ownedManagedConsumableKindInvalid";
        if (kind == LegacyConsumableItemKind.Mana && TryPrepareManagedManaConsumable(context, owner, out var mana, out reason))
        { plan = new(mana.Updated, mana.Inputs, mana.After, mana.Catalog, mana.Frames, mana.Write, null); return true; }
        if (kind == LegacyConsumableItemKind.FullRestore && TryPrepareFullRestoreConsumable(context, owner, out var restore, out reason))
        { plan = new(restore.Updated, restore.Inputs, restore.After, restore.Catalog, restore.Frames, null, restore.Write); return true; }
        return false;
    }

    private static async ValueTask HandleOwnedManagedConsumableAsync(WorldPacketHandlerContext context,
        WorldSession supplied, uint uid, LegacyConsumableItemKind kind, CancellationToken cancellationToken)
    {
        if (kind is not (LegacyConsumableItemKind.Mana or LegacyConsumableItemKind.FullRestore)) throw InvalidEquipmentMove();
        var prefix = kind == LegacyConsumableItemKind.Mana ? "managedManaConsumable" : "fullRestoreConsumable";
        ManagedManaConsumableOwner? owner = null; ManagedConsumableCommitPlan? committed = null; WorldSession? final = null;
        var reason = prefix + "OwnerUnavailable"; var durable = false;
        if (context.Trades is not { } trades || context.Sessions is not { } sessions)
        { await LogManagedConsumableAsync(context, kind, reason).ConfigureAwait(false); return; }
        try
        {
            // Actual Frame/equipment dispatch already owns the packet operation.
            // Reuse the real owner checks; never nest/upgrade its Shared lease.
            lock (trades.SyncRoot)
            lock (LegacySecondaryPasswordRuntime.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryCaptureManagedManaConsumableOwner(context, supplied, uid, out var captured))
                {
                    owner = captured;
                    if (TryPrepareManagedConsumable(context, captured, kind, out var plan, out reason))
                    {
                        plan.Inputs.Arm();
                        bool Writer()
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!ManagedManaConsumableOwnerIsCurrent(context, captured, captured.Expected) ||
                                !HealthConsumableCatalogIsCurrent(context, plan.Catalog) ||
                                !plan.After.IsCurrent(plan.Updated.PlayerItems) || !plan.Inputs.ConsumeBefore())
                            { reason = prefix + "CurrentInputsChanged"; return false; }
                            var result = plan.TryCommit(out reason);
                            if (result == ManagedConsumableOutcome.Rejected) return false;
                            if (result == ManagedConsumableOutcome.RecoveryRequired)
                            {
                                captured.Recovery.Block(prefix + "RecoveryRequired:" + reason);
                                throw new LegacyCommittedSpellPublicationException();
                            }
                            durable = true;
                            if (!plan.Inputs.ConsumeAfter() || !plan.After.IsCurrent(plan.Updated.PlayerItems))
                            {
                                captured.Recovery.Block(prefix + "RecoveryRequired:durableInputsChanged");
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
                try { owner.Recovery.Block(prefix + "RecoveryRequired:postPublication"); }
                catch (Exception secondary) { published.RecordFencingFailure(secondary); }
            throw;
        }
        catch (LegacyCommittedSpellPublicationException)
        {
            owner?.Recovery.Block(prefix + "RecoveryRequired:durablePublicationUnproven");
            await LogManagedConsumableAsync(context, kind, "recoveryRequired; no automatic retry or refund").ConfigureAwait(false);
            return;
        }
        catch (Exception error) when (LegacyEquipmentMovePersistence.IsPreparationFailure(error))
        {
            if (durable)
            {
                owner!.Recovery.Block(prefix + "RecoveryRequired:postCommitFault"); owner.Binding.Retire();
                throw;
            }
            reason = prefix + "Preparation:" + error.GetType().Name;
        }
        if (owner is null || committed is null || final is null)
        { await LogManagedConsumableAsync(context, kind, reason).ConfigureAwait(false); return; }
        try
        {
            foreach (var frame in committed.Frames)
            {
                if (!Current()) return;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!Current()) return;
            }
            await LogManagedConsumableAsync(context with { Session = final }, kind, "committedBeforeReply").ConfigureAwait(false);
        }
        catch
        {
            owner.Recovery.Block(prefix + "RecoveryRequired:committedNotificationFault"); owner.Binding.Retire();
            throw;
        }
        bool Current()
        {
            lock (trades.SyncRoot)
            lock (LegacySecondaryPasswordRuntime.SyncRoot)
                return ManagedManaConsumableOwnerIsCurrent(context, owner, final);
        }
    }

    private static Task LogManagedConsumableAsync(WorldPacketHandlerContext context, LegacyConsumableItemKind kind, string reason)
    {
        var name = kind == LegacyConsumableItemKind.Mana ? "managed Mana" : "FullRestore";
        return context.Log.WriteLineAsync($"WorldServer {name} consumable {FormatSession(context)} reason={reason}.");
    }
}
