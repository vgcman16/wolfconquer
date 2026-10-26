using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacySashTransferAsync(WorldPacketHandlerContext context,
        LegacyWarehousePacket packet, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            context.TradeOperations is not { } operations || context.TradeRecovery is not { } recovery)
        {
            await context.Log.WriteLineAsync("WorldServer skipped sash transfer; required owner unavailable.").ConfigureAwait(false);
            return;
        }
        LegacySashTransferPlan? committed = null;
        var reason = "sashTransferOwnerUnavailable";
        using (await operations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false))
        lock (trades.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryCaptureLegacySashTransferOwner(context, out var owner) &&
                recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) &&
                TryPrepareLegacySashTransfer(owner, packet, out var plan, out reason))
            {
                var failure = "sashTransferOwnerChanged";
                if (sessions.TryCommitSession(context.Connection, owner.Session, plan.Updated, () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!recovery.IsReadyFor(owner.Session.UsersRoot, context.PlayerItemsRoot) ||
                        !IsCurrentLegacySashTransferOwner(context, owner, owner.Session) ||
                        !LegacySashTransferAttributesCurrent(owner) ||
                        !HasCoherentLegacySashTrade(context, owner.Session, owner.Session.Character.Attributes) ||
                        !LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, owner.Session) ||
                        !plan.Before.IsCurrent(owner.Session.PlayerItems) || !plan.After.IsCurrent(plan.Updated.PlayerItems)) return false;
                    var saved = LegacySashTransferPersistence.TryPersist(owner.Session, plan.Updated,
                        context.PlayerItemsRoot!, plan.Before, plan.After, out failure);
                    if (!saved && failure.StartsWith(LegacySashTransferPersistence.RecoveryRequired, StringComparison.Ordinal))
                        recovery.Block(failure);
                    return saved;
                })) committed = plan;
                else reason = failure;
            }
        }

        if (committed is null)
        {
            await context.Log.WriteLineAsync($"WorldServer skipped sash transfer action={packet.Action} reason={reason}; " +
                (recovery.IsBlocked ? "durable state requires reconciliation; world fenced, success withheld."
                    : "no transfer committed.")).ConfigureAwait(false);
            return;
        }
        await context.Log.WriteLineAsync($"WorldServer sash transfer committed action={packet.Action} " +
            $"sashUid={packet.NpcId} itemUid={packet.ItemUid}; item file and Store committed, delivery pending.").ConfigureAwait(false);
        foreach (var frame in committed.Frames)
        {
            if (!IsCurrentLegacySashTransferOwner(context, committed.Owner, committed.Updated))
            {
                await context.Log.WriteLineAsync("WorldServer sash transfer delivery stopped after commit; owner retired or changed.")
                    .ConfigureAwait(false);
                return;
            }
            // A send failure propagates as postcommit failure; never roll back or replay a move.
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        var operation = packet.Action == LegacyWarehouseAction.ShashDepositItem ? "deposit" : "withdraw";
        await context.Log.WriteLineAsync($"WorldServer handled LegacyWarehouse sash {operation} " +
            $"{FormatSession(context with { Session = committed.Updated })} sashUid={packet.NpcId} itemUid={packet.ItemUid}.")
            .ConfigureAwait(false);
    }
}
