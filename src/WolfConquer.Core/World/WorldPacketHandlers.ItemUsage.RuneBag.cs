using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyDirectRuneBagSource = "MsgItemUsuagePacket.Rune";
    private const string LegacyDirectRuneBagLines = "2156-2188";

    private static async ValueTask HandleLegacyDirectRuneBagItemUseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (session.Character.Reborn < 2 || session.Character.Level < 15)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage direct rune bag " +
                    $"{FormatSession(context)} source={LegacyDirectRuneBagSource} " +
                    $"sourceLines={LegacyDirectRuneBagLines} itemUid={packet.Id}; " +
                    "source requires second reborn and level 15.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.DwParam == 0)
        {
            await HandleLegacyDirectRuneBagAddAsync(context, session, packet, frame, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await HandleLegacyDirectRuneBagRemoveAsync(context, session, packet, frame, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDirectRuneBagAddAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (!TryMoveInventoryRuneToBag(session.PlayerItems, packet.Id, out var bagItem, out var updatedItems))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage direct rune bag add " +
                    $"{FormatSession(context)} source={LegacyDirectRuneBagSource} " +
                    $"sourceLines={LegacyDirectRuneBagLines} itemUid={packet.Id}; " +
                    "inventory item was missing, rune bag already held the UID, or the bag was full.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = ClampLegacyEquipmentRuntimeHitpoints(
            session with { PlayerItems = updatedItems },
            context.WorldData);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "direct rune bag add",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        if (!IsLegacyRuneItemId(bagItem.ItemId))
        {
            await context.Connection.SendAsync(
                    new MsgItemUsagePacket(
                        bagItem.Uid,
                        DwParam: 0,
                        MsgItemUsageAction.RemoveInventory,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        bagItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(bagItem, context.WorldData?.ItemsById))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame)),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage direct rune bag add " +
                $"{FormatSession(updatedContext)} source={LegacyDirectRuneBagSource} " +
                $"sourceLines={LegacyDirectRuneBagLines} itemUid={bagItem.Uid} itemId={bagItem.ItemId} " +
                $"position={bagItem.Position}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyDirectRuneBagRemoveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage direct rune bag remove " +
                    $"{FormatSession(context)} source={LegacyDirectRuneBagSource} " +
                    $"sourceLines={LegacyDirectRuneBagLines} itemUid={packet.Id}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryMoveRuneBagItemToInventory(session.PlayerItems, packet.Id, out var returned, out var updatedItems))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage direct rune bag remove " +
                    $"{FormatSession(context)} source={LegacyDirectRuneBagSource} " +
                    $"sourceLines={LegacyDirectRuneBagLines} itemUid={packet.Id}; rune bag item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = ClampLegacyEquipmentRuntimeHitpoints(
            session with { PlayerItems = updatedItems },
            context.WorldData);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "direct rune bag remove",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                new MsgItemUsagePacket(
                    returned.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        returned,
                        LegacyGameItemPacket.CreateRuneSpellOverride(returned, context.WorldData?.ItemsById))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                CreateLegacyItemUsageFrameWithArgs(packet, ReadLegacyItemUsageArgs(frame)),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage direct rune bag remove " +
                $"{FormatSession(updatedContext)} source={LegacyDirectRuneBagSource} " +
                $"sourceLines={LegacyDirectRuneBagLines} itemUid={returned.Uid} itemId={returned.ItemId} " +
                $"position={returned.Position}.")
            .ConfigureAwait(false);
    }

    private static bool TryMoveInventoryRuneToBag(
        PlayerItemSnapshot items,
        uint uid,
        out PlayerItemSnapshotItem bagItem,
        out PlayerItemSnapshot updatedItems)
    {
        bagItem = null!;
        updatedItems = items;
        var inventoryItem = items.InventoryItems.FirstOrDefault(item => item.Uid == uid);
        if (inventoryItem is null ||
            items.RuneItems.Any(item => item.Uid == inventoryItem.Uid) ||
            items.RuneItems.Count(item => item.Position == LegacyRuneBagPosition) >= 100)
        {
            return false;
        }

        bagItem = inventoryItem.WithPosition(LegacyRuneBagPosition);
        updatedItems = items with
        {
            InventoryItems = items.InventoryItems.Where(item => item.Uid != inventoryItem.Uid).ToArray(),
            RuneItems = items.RuneItems
                .Concat([bagItem])
                .OrderBy(item => item.Position)
                .ThenBy(item => IsLegacyRuneBagPosition(item.Position) ? 0u : item.Uid)
                .ToArray(),
        };
        return true;
    }

    private static bool TryMoveRuneBagItemToInventory(
        PlayerItemSnapshot items,
        uint uid,
        out PlayerItemSnapshotItem inventoryItem,
        out PlayerItemSnapshot updatedItems)
    {
        inventoryItem = null!;
        updatedItems = items;
        var bagItem = items.RuneItems.FirstOrDefault(item =>
            item.Uid == uid &&
            item.Position == LegacyRuneBagPosition);
        if (bagItem is null)
        {
            return false;
        }

        inventoryItem = bagItem.WithPosition(0);
        updatedItems = items with
        {
            InventoryItems = items.InventoryItems.Concat([inventoryItem]).ToArray(),
            RuneItems = items.RuneItems.Where(item => item.Uid != bagItem.Uid).ToArray(),
        };
        return true;
    }

    private static bool IsLegacyRuneItemId(uint itemId)
    {
        var runeGroup = itemId / 10_000;
        return runeGroup is >= 401 and <= 403;
    }
}
