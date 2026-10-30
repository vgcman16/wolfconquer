using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyRuneBagFullInventoryAddFailureAsync(
        WorldPacketHandlerContext context, WorldSession session, MsgItemUsagePacket packet,
        PacketFrame frame, CancellationToken cancellationToken)
    {
        if (session.PlayerItems.InventoryItems.Count != 40 ||
            HasLegacyInventorySpace(session.PlayerItems, 1))
            return false;

        var bagItem = session.PlayerItems.RuneItems.FirstOrDefault(item =>
            item.Uid == packet.Id && item.Position == LegacyRuneBagPosition);
        if (bagItem is null || bagItem.ItemId != 4_020_517 || bagItem.StackSize != 1 ||
            bagItem.Bound > 1 ||
            !TryReadLegacyItemType(context.WorldData, bagItem.ItemId, out var itemType) ||
            itemType.RequiredClass != 6_000 || itemType.Fields.Count <= 46 ||
            !ushort.TryParse(itemType.Fields[46], out var stackLimit) || stackLimit != 0 ||
            !uint.TryParse(itemType.Fields[38], out var time) || time != 0)
            return false;

        // Native Remove(false) already deletes the bag item. With cap0/full40, inner Add fails
        // before UID/random/clock; its caller ignores that false, then echoes and updates rank.
        // This is the destructive reference edge, not a successful return or protective repair.
        var updatedItems = session.PlayerItems with
        {
            RuneItems = session.PlayerItems.RuneItems.Where(item => item.Uid != bagItem.Uid).ToArray(),
        };
        var updatedSession = ClampLegacyEquipmentRuntimeHitpoints(
            session with { PlayerItems = updatedItems }, context.WorldData);
        updatedSession = PublishLegacyItemUsageSession(context, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        // Selected managed file compatibility, not native Save/SQL/send-interleaving equivalence.
        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext, updatedSession, "direct rune bag full inventory add failure",
                characterValues: null, persistItems: true)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(new MsgItemUsagePacket(
                bagItem.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame)),
                cancellationToken)
            .ConfigureAwait(false);
        UpdateLegacyRuneRanking(updatedContext, updatedSession);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage direct rune bag full inventory add failure " +
                $"{FormatSession(updatedContext)} removedUid={bagItem.Uid} itemId={bagItem.ItemId}; " +
                "native Add returned false after removal; no inventory item was created.")
            .ConfigureAwait(false);
        return true;
    }
}
