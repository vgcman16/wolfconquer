using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyRuneBagExistingStackMergeAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgItemUsagePacket packet,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        var bagItem = session.PlayerItems.RuneItems.FirstOrDefault(item =>
            item.Uid == packet.Id && item.Position == LegacyRuneBagPosition);
        // Deliberately bounded to the real ordinary RedRuneFragment row, not general return parity.
        if (bagItem is null || bagItem.ItemId != 3_306_366 || bagItem.Bound > 1 ||
            !TryReadLegacyItemType(context.WorldData, bagItem.ItemId, out var itemType) ||
            itemType.RequiredClass != 0 || itemType.Fields.Count <= 46 ||
            !ushort.TryParse(itemType.Fields[46], out var stackLimit) || stackLimit == 0 ||
            !uint.TryParse(itemType.Fields[38], out var time) || time != 0 ||
            itemType.LegacyDurability == 0)
            return false;

        PlayerItemSnapshotItem? target = null;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != bagItem.ItemId || item.Bound != bagItem.Bound ||
                (uint)item.StackSize + bagItem.StackSize > stackLimit)
                continue;
            // Count native fitting candidates before checking ordinary-field restrictions.
            if (target is not null)
                return false;
            target = item;
        }
        if (target is null || target.Uid == bagItem.Uid || target.Position != 0 ||
            target.Activate != 0 || target.Expiration != 0 ||
            target.Plus != 0 || target.MaximumDurability != itemType.LegacyDurability ||
            target.PurificationItemId != 0 || target.PurificationLevel != 0 ||
            target.PurificationDuration != 0 || target.PurificationAddedOn != 0 ||
            target.RefineryEffectId != 0 || target.RefineryEffectLevel != 0 ||
            target.RefineryEffectPercent != 0 || target.RefineryEffectPercent2 != 0 ||
            target.RefineryEffectDuration != 0 || target.RefineryEffectAddedOn != 0 ||
            target.UnlockTimer != 0)
            return false;

        var merged = target.WithStackSize(checked((ushort)(target.StackSize + bagItem.StackSize)))
            .WithLocked(0);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems
                .Select(item => item.Uid == target.Uid ? merged : item).ToArray(),
            RuneItems = session.PlayerItems.RuneItems.Where(item => item.Uid != bagItem.Uid).ToArray(),
        };
        var updatedSession = ClampLegacyEquipmentRuntimeHitpoints(
            session with { PlayerItems = updatedItems }, context.WorldData);
        updatedSession = PublishLegacyItemUsageSession(context, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        // Selected managed file compatibility; not native in-loop Send/Save/SQL timing.
        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext, updatedSession, "direct rune bag existing stack merge",
                characterValues: null, persistItems: true)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(new MsgItemUsagePacket(
                bagItem.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(
                    merged, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame)),
                cancellationToken)
            .ConfigureAwait(false);
        UpdateLegacyRuneRanking(updatedContext, updatedSession);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage direct rune bag existing stack merge " +
                $"{FormatSession(updatedContext)} removedUid={bagItem.Uid} existingUid={merged.Uid} " +
                $"itemId={merged.ItemId} stack={merged.StackSize}.")
            .ConfigureAwait(false);
        return true;
    }
}
