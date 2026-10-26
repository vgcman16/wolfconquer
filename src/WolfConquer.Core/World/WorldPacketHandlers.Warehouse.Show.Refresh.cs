namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Ordinary Show can perform SendItemLocked's durable refresh. Frame delegates
    // its operation lease here; physical-bag Show retains its ordinary outer Shared.
    private static async ValueTask HandleLegacyWarehouseShowWithLockRefreshAsync(WorldPacketHandlerContext context,
        LegacyWarehousePacket request, CancellationToken cancellationToken)
    {
        if (request.Action == LegacyWarehouseAction.ShashShow)
        {
            await HandleLegacyWarehouseShowAsync(context, request, cancellationToken).ConfigureAwait(false);
            return;
        }

        var operations = context.TradeOperations;
        using (operations is null ? null : await operations.EnterPacketSharedAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.TradeRecovery?.IsBlocked == true)
            {
                await context.Log.WriteLineAsync("WorldServer skipped LegacyWarehouse show; recovery is blocked.").ConfigureAwait(false);
                return;
            }
            if (!TryCaptureLegacyWarehouseShowOwner(context, out var owner) || !CanShowLegacyWarehouse(owner, request))
            {
                await context.Log.WriteLineAsync("WorldServer skipped LegacyWarehouse show; current owner or target unavailable.")
                    .ConfigureAwait(false);
                return;
            }
            var now = DateTime.Now;
            var needsRefresh = false;
            if (LegacyServerIdentityRuntime.IsOnOwnServer(context.Connection, owner.Session, context.LocalServerIdentity))
            {
                if (!TryProjectLegacyWarehouseShowLocks(owner, request, now, out var unlocked, out var failure))
                {
                    await context.Log.WriteLineAsync($"WorldServer skipped LegacyWarehouse lock refresh; reason={failure}.")
                        .ConfigureAwait(false);
                    return;
                }
                needsRefresh = unlocked.Count != 0;
            }
            if (!needsRefresh)
            {
                // Preserve existing read-only locked/future-date presentation.
                // Reuse this owner and timestamp rather than reselect after probe.
                if (!TryPrepareLegacyWarehouseShow(owner, request, out var frames, out var count, now))
                {
                    await context.Log.WriteLineAsync("WorldServer skipped LegacyWarehouse show; complete wire image unavailable.")
                        .ConfigureAwait(false);
                    return;
                }
                await SendLegacyWarehouseShowAsync(context, owner, request, frames, count, cancellationToken).ConfigureAwait(false);
                return;
            }
        } // Fully release Shared BEFORE waiting for Exclusive; never upgrade/nest.

        if (operations is null || context.Trades is not { } trades ||
            context.TradeRecovery is not { } recovery || context.Sessions is null)
        {
            await context.Log.WriteLineAsync("WorldServer skipped LegacyWarehouse lock refresh; required owner unavailable.")
                .ConfigureAwait(false);
            return;
        }
        LegacyWarehouseLockRefreshDelivery? delivery;
        string reason;
        using (await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false))
        lock (trades.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            delivery = TryCommitLegacyWarehouseShowLocks(context, request, cancellationToken, out reason);
        }
        if (delivery is null)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyWarehouse lock refresh; reason={reason}; " +
                (recovery.IsBlocked ? "durable state requires reconciliation; world fenced, success withheld."
                    : "no lock refresh committed.")).ConfigureAwait(false);
            return;
        }
        if (delivery.Committed)
            await context.Log.WriteLineAsync("WorldServer warehouse lock refresh committed; item file and Store committed, delivery pending.")
                .ConfigureAwait(false);
        // Both committed and freshly reselected no-op frames are immutable.
        // No Exclusive or reacquired Shared is retained across slow notification.
        await SendLegacyWarehouseShowAsync(context, delivery.Owner, request, delivery.Frames, delivery.Count,
            cancellationToken, requireRefreshOwner: delivery.Committed).ConfigureAwait(false);
    }
}
