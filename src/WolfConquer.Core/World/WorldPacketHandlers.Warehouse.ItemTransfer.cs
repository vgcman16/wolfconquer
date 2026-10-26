using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyWarehouseItemTransferAsync(WorldPacketHandlerContext context,
        LegacyWarehousePacket packet, CancellationToken cancellationToken)
    {
        var physicalSash = LegacyWarehouseItemTransferActions.IsPhysicalSash(packet.Action);
        var label = physicalSash ? "sash transfer" : "warehouse item transfer";
        var location = physicalSash ? "sashUid" : "npcId";
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            context.TradeOperations is not { } operations || context.TradeRecovery is not { } recovery)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped {label}; required owner unavailable.").ConfigureAwait(false);
            return;
        }
        LegacyWarehouseItemTransferPlan? committed = null;
        var reason = "warehouseItemTransferOwnerUnavailable";
        using (await operations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false))
        lock (trades.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryCaptureLegacyWarehouseItemTransferOwner(context, out var owner) &&
                recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) &&
                TryPrepareLegacyWarehouseItemTransfer(owner, packet, out var plan, out reason))
            {
                var failure = "warehouseItemTransferOwnerChanged";
                if (sessions.TryCommitSession(context.Connection, owner.Session, plan.Updated, () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) ||
                        !IsCurrentLegacyWarehouseItemTransferOwner(context, owner, owner.Session) ||
                        !LegacyWarehouseItemTransferAttributesCurrent(owner) ||
                        !HasCoherentLegacyWarehouseTransferTrade(context, owner.Session, owner.Session.Character.Attributes) ||
                        !LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, owner.Session) ||
                        !plan.Before.IsCurrent(owner.Session.PlayerItems) || !plan.After.IsCurrent(plan.Updated.PlayerItems)) return false;
                    var saved = LegacyWarehouseItemTransferPersistence.TryPersist(owner.Session, plan.Updated,
                        context.PlayerItemsRoot!, plan.Before, plan.After, plan.InventorySashCount, out failure);
                    if (!saved && failure.StartsWith(LegacyWarehouseItemTransferPersistence.RecoveryRequired, StringComparison.Ordinal))
                        recovery.Block(failure);
                    return saved;
                })) committed = plan;
                else reason = failure;
            }
        }

        if (committed is null)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped {label} action={packet.Action} reason={reason}; " +
                (recovery.IsBlocked ? "durable state requires reconciliation; world fenced, success withheld."
                    : "no transfer committed.")).ConfigureAwait(false);
            return;
        }
        await context.Log.WriteLineAsync($"WorldServer {label} committed action={packet.Action} " +
            $"{location}={packet.NpcId} itemUid={packet.ItemUid}; item file and Store committed, delivery pending.").ConfigureAwait(false);
        foreach (var frame in committed.Frames)
        {
            if (!IsCurrentLegacyWarehouseItemTransferOwner(context, committed.Owner, committed.Updated))
            {
                await context.Log.WriteLineAsync($"WorldServer {label} delivery stopped after commit; owner retired or changed.")
                    .ConfigureAwait(false);
                return;
            }
            // A send failure propagates as postcommit failure; never roll back or replay a move.
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        var operation = LegacyWarehouseItemTransferActions.IsDeposit(packet.Action) ? "deposit" : "withdraw";
        var family = physicalSash ? "sash " : string.Empty;
        await context.Log.WriteLineAsync($"WorldServer handled LegacyWarehouse {family}{operation} " +
            $"{FormatSession(context with { Session = committed.Updated })} {location}={packet.NpcId} itemUid={packet.ItemUid}.")
            .ConfigureAwait(false);
    }
}
