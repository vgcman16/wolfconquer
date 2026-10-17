using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyMythsoulBagPosition = 217;
    private const string LegacyMythsoulBagSource = "MsgItemUsuagePacket.MythForggingMythsoulBagAdd/Remove";
    private const string LegacyMythsoulBagLines = "325-370";

    private static async ValueTask HandleLegacyMythsoulBagAddAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        var item = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.Id);
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage mythsoul bag add " +
                    $"{FormatSession(context)} source={LegacyMythsoulBagSource} " +
                    $"sourceLines={LegacyMythsoulBagLines} itemUid={packet.Id}; inventory item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var args = ReadLegacyItemUsageArgs(frame);
        var updatedItems = session.PlayerItems;
        var mutationFrames = new List<byte[]>();
        var replacedItemUid = 0U;
        var movedReplacement = false;
        var movedRequested = false;
        var sentReplacementEcho = false;

        var replacement = updatedItems.MythsoulBagItems.FirstOrDefault(candidate =>
            GetLegacyMythsoulBaseItemId(candidate.ItemId) == GetLegacyMythsoulBaseItemId(item.ItemId));
        if (replacement is not null)
        {
            if (!HasLegacyInventorySpace(updatedItems, 1))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage mythsoul bag add " +
                        $"{FormatSession(context)} source={LegacyMythsoulBagSource} " +
                        $"sourceLines={LegacyMythsoulBagLines} itemUid={item.Uid} itemId={item.ItemId} " +
                        $"replacementUid={replacement.Uid}; inventory is full.")
                    .ConfigureAwait(false);
                return;
            }

            replacedItemUid = replacement.Uid;
            if (IsLegacyMythsoulItem(replacement.ItemId))
            {
                var returned = replacement.WithPosition(0);
                updatedItems = MoveMythsoulBagItemToInventory(updatedItems, replacement, returned);
                mutationFrames.Add(
                    LegacyGameItemPacket
                        .FromSnapshotItem(returned, mode: LegacyGameItemPacket.UpdateItemMode)
                        .ToFrameBytes());
                movedReplacement = true;
            }

            var echo = packet with
            {
                Id = replacement.Uid,
                Action = MsgItemUsageAction.MythForggingMythsoulBagRemove,
            };
            mutationFrames.Add(CreateLegacyItemUsageFrameWithArgs(echo, args));
            sentReplacementEcho = true;
        }

        if (IsLegacyMythsoulItem(item.ItemId))
        {
            var bagItem = item.WithPosition(LegacyMythsoulBagPosition);
            updatedItems = MoveInventoryItemToMythsoulBag(updatedItems, item, bagItem);
            mutationFrames.Add(
                LegacyGameItemPacket
                    .FromSnapshotItem(bagItem, mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes());
            movedRequested = true;
        }

        if (!movedReplacement && !movedRequested && !sentReplacementEcho)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage mythsoul bag add " +
                    $"{FormatSession(context)} source={LegacyMythsoulBagSource} " +
                    $"sourceLines={LegacyMythsoulBagLines} itemUid={item.Uid} itemId={item.ItemId}; " +
                    "source only stores item family 407 in the mythsoul bag.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { PlayerItems = updatedItems };
        if (movedReplacement || movedRequested)
        {
            updatedSession = ClampLegacyEquipmentRuntimeHitpoints(updatedSession, context.WorldData);
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };
            await TryPersistLegacyItemUsageMutationAsync(
                    updatedContext,
                    updatedSession,
                    "mythsoul bag add",
                    characterValues: null,
                    persistItems: true)
                .ConfigureAwait(false);
        }

        foreach (var mutationFrame in mutationFrames)
        {
            await context.Connection.SendAsync(mutationFrame, cancellationToken).ConfigureAwait(false);
        }

        if (movedReplacement || movedRequested)
        {
            await SendLegacyEquipmentRuntimeRefreshAsync(
                    context with { Session = updatedSession },
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage mythsoul bag add " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyMythsoulBagSource} " +
                $"sourceLines={LegacyMythsoulBagLines} itemUid={item.Uid} itemId={item.ItemId} " +
                $"movedRequested={movedRequested} movedReplacement={movedReplacement} " +
                $"sentReplacementEcho={sentReplacementEcho} replacedItemUid={replacedItemUid}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyMythsoulBagRemoveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage mythsoul bag remove " +
                    $"{FormatSession(context)} source={LegacyMythsoulBagSource} " +
                    $"sourceLines={LegacyMythsoulBagLines} itemUid={packet.Id}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        var item = session.PlayerItems.MythsoulBagItems.FirstOrDefault(item => item.Uid == packet.Id);
        if (item is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage mythsoul bag remove " +
                    $"{FormatSession(context)} source={LegacyMythsoulBagSource} " +
                    $"sourceLines={LegacyMythsoulBagLines} itemUid={packet.Id}; mythsoul bag item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacyMythsoulItem(item.ItemId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage mythsoul bag remove " +
                    $"{FormatSession(context)} source={LegacyMythsoulBagSource} " +
                    $"sourceLines={LegacyMythsoulBagLines} itemUid={item.Uid} itemId={item.ItemId}; " +
                    "source only removes item family 407 from the mythsoul bag.")
                .ConfigureAwait(false);
            return;
        }

        var returned = item.WithPosition(0);
        var updatedSession = session with
        {
            PlayerItems = MoveMythsoulBagItemToInventory(session.PlayerItems, item, returned),
        };
        updatedSession = ClampLegacyEquipmentRuntimeHitpoints(updatedSession, context.WorldData);
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "mythsoul bag remove",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(returned, mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage mythsoul bag remove " +
                $"{FormatSession(updatedContext)} source={LegacyMythsoulBagSource} " +
                $"sourceLines={LegacyMythsoulBagLines} itemUid={item.Uid} itemId={item.ItemId}.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshot MoveInventoryItemToMythsoulBag(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem inventoryItem,
        PlayerItemSnapshotItem bagItem)
    {
        return items with
        {
            InventoryItems = items.InventoryItems.Where(item => item.Uid != inventoryItem.Uid).ToArray(),
            MythsoulBagItems = items.MythsoulBagItems.Concat([bagItem]).ToArray(),
        };
    }

    private static PlayerItemSnapshot MoveMythsoulBagItemToInventory(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem bagItem,
        PlayerItemSnapshotItem inventoryItem)
    {
        return items with
        {
            InventoryItems = items.InventoryItems.Concat([inventoryItem]).ToArray(),
            MythsoulBagItems = items.MythsoulBagItems.Where(item => item.Uid != bagItem.Uid).ToArray(),
        };
    }

    private static uint GetLegacyMythsoulBaseItemId(uint itemId)
    {
        return itemId - (itemId % 10);
    }

    private static bool IsLegacyMythsoulItem(uint itemId)
    {
        return itemId / 100_000 == 407;
    }
}
