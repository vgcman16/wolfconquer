using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyFloorItem)]
    private static async ValueTask HandleLegacyFloorItemAsync(WorldPacketHandlerContext context,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LegacyFloorPickupPlan? committed = null;
        WorldSession? admitted = null;
        LegacyNativePlayerRuntime? pickupPlayer = null;
        byte[]? rejection = null;
        uint floorUid = 0;
        var reason = "currentSessionMissing";
        var durable = false;
        var teamOwnerLock = false;
        // Pickup owns its exclusive lease, just like Drop/Sell. No socket send
        // may run during reservation, persistence or current-holder publication.
        using (var operation = context.TradeOperations is { } operations
            ? await operations.EnterExclusiveAsync(cancellationToken).ConfigureAwait(false) : null)
        {
            if (!TryAdmitLegacyFloorPickup(context, out var current, out reason)) { }
            else if (!LegacyFloorItemPacket.TryParsePickupRequest(frame, out floorUid)) reason = "malformedPickupRequest";
            else
            {
                context = context with { Session = current };
                admitted = current;
                // Keep the exact physical owner across sends for quest1311.
                pickupPlayer = current.NativePlayer;
                if (TryPlanLegacyFloorPickup(context, current, floorUid, out var plan, out var ownerBypass,
                    out reason, out rejection))
                {
                    var status = context.FloorItems!.TryReservePickup(plan.Entry,
                        checked((uint)current.Character.Uid), ownerBypass, DateTimeOffset.UtcNow, out var reservation);
                    using (reservation)
                    {
                        if (status != FloorItemRuntimePickupStatus.Available) reason = "pickupReservation:" + status;
                        else if (TryCommitLegacyFloorPickup(context, current, plan, reservation!, out durable, out reason))
                        { committed = plan; teamOwnerLock = ownerBypass; }
                    }
                }
            }
        }
        if (committed is null)
        {
            if (rejection is not null)
                await context.Connection.SendAsync(rejection, cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyFloorItem pickup {FormatSession(context)} " +
                $"floorUid={floorUid}; {reason}.").ConfigureAwait(false);
            return;
        }

        var updatedContext = context with { Session = committed.Updated };
        if (teamOwnerLock)
            await context.Log.WriteLineAsync($"WorldServer allowed LegacyFloorItem team owner-lock pickup {FormatSession(context)} " +
                $"floorUid={floorUid} ownerUid={committed.Entry.Packet.ItemOwnerUid} kind={committed.Entry.Kind}.").ConfigureAwait(false);
        foreach (var bytes in committed.BeforeRemovalFrames)
            await context.Connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
        await SendLegacyFloorItemRemoveAsync(updatedContext, committed.Updated.Character,
            committed.Entry, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(committed.PickupTextFrame, cancellationToken).ConfigureAwait(false);
        if (committed.CreditsQuest1311)
            await IncreaseLegacyFloorPickupQuest1311Async(updatedContext, admitted!, pickupPlayer, cancellationToken)
                .ConfigureAwait(false);
        var kind = committed.Entry.Kind switch
        { FloorItemRuntimePickupKind.Money => "money", FloorItemRuntimePickupKind.ConquerPoints => "Conquer Point", _ => "item" };
        var walletLog = committed.Entry.Kind == FloorItemRuntimePickupKind.ConquerPoints
            ? $" updatedConquerPoints={committed.Updated.Character.ConquerPoints} walletUpdate=LegacyMsgUpdate.ConquerPoints" : string.Empty;
        await context.Log.WriteLineAsync($"WorldServer handled LegacyFloorItem {kind} pickup {FormatSession(updatedContext)} " +
            $"floorUid={floorUid} kind={committed.Entry.Kind} amount={committed.Entry.Amount} " +
            $"itemUid={committed.Item?.Uid} persistedBeforeReply={durable}{walletLog}.").ConfigureAwait(false);
    }
}
