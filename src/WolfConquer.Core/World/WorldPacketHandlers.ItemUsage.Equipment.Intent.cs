using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Frame delegates action4/6 operation ownership here. Never upgrade a held
    // Shared lease: preserve non-equipment behavior under Shared, then release
    // and resolve a fresh ordinary gear owner under Exclusive.
    private static async ValueTask HandleOwnedEquipmentIntentAsync(WorldPacketHandlerContext context,
        MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        if (context.TradeOperations is not { } operations)
        { await LogEquipmentMoveRefusalAsync(context, "equipmentMoveOperationOwnerUnavailable").ConfigureAwait(false); return; }
        if (packet.Action == MsgItemUsageAction.Equip)
        {
            using (await operations.EnterPacketSharedAsync(cancellationToken).ConfigureAwait(false))
            {
                if (context.TradeRecovery is not { } recovery || recovery.IsBlocked ||
                    !recovery.TryRequireLiveJournalClear(out _) || context.Sessions is not { } sessions ||
                    !sessions.TryGet(context.Connection, out var current) || current is null)
                { await LogEquipmentMoveRefusalAsync(context, "equipmentMoveOwnerUnavailable").ConfigureAwait(false); return; }
                var active = context with { Session = current };
                if (await TryHandleLegacyConsumableItemUseAsync(active, current, packet, cancellationToken).ConfigureAwait(false)) return;
                var item = current.PlayerItems.InventoryItems.FirstOrDefault(row => row.Uid == packet.Id);
                if (item is null)
                { await LogEquipmentMoveRefusalAsync(active, "equipmentMoveInventoryOwnerMissing").ConfigureAwait(false); return; }
                if (ItemType.GetLegacyEquipmentPosition(item.ItemId) == 0)
                {
                    await HandleNonEquipmentEquipIntentAsync(active, current, item, packet, cancellationToken).ConfigureAwait(false);
                    return;
                }
            }
        }
        await CommitAndDeliverEquipmentMoveAsync(context, packet, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleNonEquipmentEquipIntentAsync(WorldPacketHandlerContext context,
        WorldSession session, PlayerItemSnapshotItem item, MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        if (packet.DwParam == 0 &&
            await TryHandleLegacyNonEquipmentItemUseAsync(context, session, item, cancellationToken).ConfigureAwait(false)) return;
        await LogParityGapAsync(context, LegacyParityGapDiagnostics.UnsupportedItemUse(item.ItemId,
            action: (uint)packet.Action, actionName: packet.Action.ToString(), itemUid: item.Uid,
            source: "MsgItemUsage.UseItem", reason: "non-equipment item effects are not modeled")).ConfigureAwait(false);
        await context.Log.WriteLineAsync("WorldServer deferred MsgItemUsage item use " +
            $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; non-equipment item effects are not modeled.")
            .ConfigureAwait(false);
    }

    private static Task LogEquipmentMoveRefusalAsync(WorldPacketHandlerContext context, string reason) =>
        context.Log.WriteLineAsync($"WorldServer skipped MsgItemUsage equipment {FormatSession(context)} reason={reason}; " +
            (context.TradeRecovery?.IsBlocked == true ? "recovery fenced; success withheld." : "no equipment move committed."));
}
