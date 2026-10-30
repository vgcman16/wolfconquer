namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleDropItemAsync(WorldPacketHandlerContext context,
        WorldSession supplied, MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        var reason = "currentSessionMissing";
        LegacyItemDropPlan? committed = null;
        WorldSession? committedSession = null;
        FloorItemRuntimeEntry? floorEntry = null;
        byte[]? rejection = null;
        var durable = false;
        // Like Trade/Sell, Drop owns its mutation lease. Dispatch must not retain
        // a shared lease here, and no socket send may run inside this phase.
        using (var operation = context.TradeOperations is { } operations
            ? await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false) : null)
        {
            if (context.TradeRecovery?.IsBlocked == true) reason = "recoveryRequired";
            else if (context.Trades?.HasConnectionEnded(context.Connection) == true) reason = "connectionEnded";
            else if (!TryAdmitLegacySecondaryPasswordItemUsage(context, supplied, out var current, out reason)) { }
            else if (TryPlanLegacyItemDrop(context, current, packet, out var plan, out reason, out rejection))
            {
                FloorItemRuntimeStateStore.GroundReservation? reservation = null;
                try
                {
                    if (plan.GroundDrop && !TryReserveLegacyItemDrop(context, current, plan, out reservation, out reason)) { }
                    else
                    {
                        // Validate all outbound representation before touching disk.
                        _ = reservation?.Entry.Packet.ToFrameBytes();
                        if (TryCommitLegacyItemDrop(context, current, plan, reservation, out durable, out reason, out committedSession))
                        {
                            committed = plan;
                            floorEntry = reservation?.Entry;
                        }
                    }
                }
                finally { reservation?.Dispose(); }
            }
        }
        if (rejection is not null)
            await context.Connection.SendAsync(rejection, cancellationToken).ConfigureAwait(false);
        if (committed is not null && committedSession is not null)
        {
            var updatedContext = context with { Session = committedSession };
            // Native network order remains floor-visible then RemoveInventory;
            // the managed disk/session/ground commit precedes both sends.
            if (floorEntry is not null)
                await SendFloorItemVisibleAsync(updatedContext, committedSession.Character, floorEntry, cancellationToken)
                    .ConfigureAwait(false);
            await context.Connection.SendAsync(committed.RemovalFrame, cancellationToken).ConfigureAwait(false);
            var kind = committed.GroundDrop ? "ground drop" : committed.Item.Bound >= 1 ? "bound drop" : "unable-trade discard";
            await context.Log.WriteLineAsync($"WorldServer handled MsgItemUsage {kind} {FormatSession(updatedContext)} " +
                $"itemUid={committed.Item.Uid} itemId={committed.Item.ItemId}; persistedBeforeReply={durable}.").ConfigureAwait(false);
        }
        else if (reason == "anima-pin-not-activated")
            await context.Log.WriteLineAsync("WorldServer blocked SecurityAnima item operation " +
                $"{FormatSession(context)} source={LegacySecurityAnimaDropItemSource} reason={reason} " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode}.").ConfigureAwait(false);
        else
            await context.Log.WriteLineAsync($"WorldServer skipped MsgItemUsage drop {FormatSession(context)}; {reason}.")
                .ConfigureAwait(false);
    }
}
