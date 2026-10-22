using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyCreateSocketItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        var args = ReadLegacyItemUsageArgs(frame);
        if (packet.DwParam2 is 1 or 5 or 7 or 12)
        {
            args = args.Take(checked((int)packet.DwParam2)).ToArray();
        }

        if (packet.DwParam2 is not (1 or 5 or 7 or 12))
        {
            await SendLegacyCreateSocketItemResponseAsync(context, 0, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage create socket item " +
                    $"{FormatSession(context)} itemUid={packet.Id} dwParam2={packet.DwParam2}; " +
                    "unsupported legacy CreateSocketItem material count.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindLegacySocketTarget(session.PlayerItems, packet.Id, out var target, out var targetIsEquipment))
        {
            await SendLegacyCreateSocketItemResponseAsync(context, 0, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage create socket item " +
                    $"{FormatSession(context)} itemUid={packet.Id}; target item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (!IsLegacySocketUpdateAllowed(target.ItemId))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTopLeft("Sorry, this item cannot be socketed!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId}; " +
                    "legacy ItemType.AllowToUpdate blocks this equipment position.")
                .ConfigureAwait(false);
            return;
        }

        switch (packet.DwParam2)
        {
            case 1:
                if (target.SocketOne == LegacyNoSocket)
                {
                    await HandleLegacyMeteorCreateSocketItemAsync(
                            context,
                            session,
                            target,
                            targetIsEquipment,
                            args,
                            materialCount: 1,
                            openFirstSocket: true,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                await HandleLegacyToughDrillCreateSocketItemAsync(
                        context,
                        session,
                        target,
                        targetIsEquipment,
                        args,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case 5:
                await HandleLegacyMeteorCreateSocketItemAsync(
                        context,
                        session,
                        target,
                        targetIsEquipment,
                        args,
                        materialCount: 5,
                        openFirstSocket: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case 7:
                await HandleLegacyStarDrillCreateSocketItemAsync(
                        context,
                        session,
                        target,
                        targetIsEquipment,
                        args,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;

            case 12:
                await HandleLegacyMeteorCreateSocketItemAsync(
                        context,
                        session,
                        target,
                        targetIsEquipment,
                        args,
                        materialCount: 12,
                        openFirstSocket: true,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
        }
    }

    private static async ValueTask HandleLegacyMeteorCreateSocketItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem target,
            bool targetIsEquipment,
            IReadOnlyList<uint> args,
            int materialCount,
            bool openFirstSocket,
            CancellationToken cancellationToken)
    {
        var socketName = openFirstSocket ? "first" : "second";
        if (openFirstSocket && target.SocketOne != LegacyNoSocket)
        {
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage meteor create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId} " +
                    $"socketOne={target.SocketOne}; legacy requires no first socket.")
                .ConfigureAwait(false);
            return;
        }

        if (!openFirstSocket && target.SocketTwo != LegacyNoSocket)
        {
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage meteor create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId} " +
                    $"socketTwo={target.SocketTwo}; legacy requires no second socket.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryConsumeLegacySocketMaterials(
                session.PlayerItems,
                args,
                LegacySocketMeteorItemId,
                materialCount,
                out var consumedMaterialItems,
                out var materialFrames))
        {
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage meteor create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId}; " +
                    $"required {materialCount} materials {LegacySocketMeteorItemId} were not present in item usage args.")
                .ConfigureAwait(false);
            return;
        }

        var updatedTarget = openFirstSocket
            ? CopyLegacyItemWithSocketOne(target, LegacyEmptySocket)
            : CopyLegacyItemWithSocketTwo(target, LegacyEmptySocket);
        var updatedItems = ReplaceLegacySocketTarget(
            consumedMaterialItems,
            target,
            updatedTarget,
            targetIsEquipment);
        var updated = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updated);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(updatedTarget, mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var materialFrame in materialFrames)
        {
            await context.Connection.SendAsync(materialFrame, cancellationToken).ConfigureAwait(false);
        }

        await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: true, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage create socket item " +
                $"{FormatSession(context with { Session = updated })} itemUid={target.Uid} itemId={target.ItemId} " +
                $"materialItemId={LegacySocketMeteorItemId} count={materialCount} socket={socketName} result=success.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyToughDrillCreateSocketItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem target,
            bool targetIsEquipment,
            IReadOnlyList<uint> args,
            CancellationToken cancellationToken)
    {
        if (target.SocketOne == LegacyNoSocket || target.SocketTwo != LegacyNoSocket)
        {
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage tough drill create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId} " +
                    $"socketOne={target.SocketOne} socketTwo={target.SocketTwo}; legacy requires an existing first socket and no second socket.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryConsumeLegacySocketMaterials(
                session.PlayerItems,
                args,
                LegacyToughDrillItemId,
                count: 1,
                out var consumedMaterialItems,
                out var materialFrames))
        {
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage tough drill create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId}; " +
                    $"required material {LegacyToughDrillItemId} was not present in item usage args.")
                .ConfigureAwait(false);
            return;
        }

        var roll = ReadLegacyToughDrillSocketRoll(session.Character, out var forcedRoll);
        if (roll < 20)
        {
            var updatedTarget = CopyLegacyItemWithSocketTwo(target, LegacyEmptySocket);
            var updatedItems = ReplaceLegacySocketTarget(
                consumedMaterialItems,
                target,
                updatedTarget,
                targetIsEquipment);
            var updated = session with { PlayerItems = updatedItems };
            context.Sessions?.Set(context.Connection, updated);

            await context.Connection.SendAsync(
                    LegacyGameItemPacket
                        .FromSnapshotItem(updatedTarget, mode: LegacyGameItemPacket.UpdateItemMode)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var materialFrame in materialFrames)
            {
                await context.Connection.SendAsync(materialFrame, cancellationToken).ConfigureAwait(false);
            }

            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: true, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled MsgItemUsage create socket item " +
                    $"{FormatSession(context with { Session = updated })} itemUid={target.Uid} itemId={target.ItemId} " +
                    $"materialItemId={LegacyToughDrillItemId} count=1 roll={roll} forcedRoll={forcedRoll} result=success.")
                .ConfigureAwait(false);
            return;
        }

        var starDrill = CreateLegacyStarDrillReward(consumedMaterialItems);
        var failedItems = new PlayerItemSnapshot(
            consumedMaterialItems.InventoryItems.Concat([starDrill]).OrderBy(item => item.Uid).ToArray(),
            consumedMaterialItems.RelicFuseItems,
            consumedMaterialItems.MythsoulBagItems,
            consumedMaterialItems.EquipmentItems,
            consumedMaterialItems.RuneItems,
            consumedMaterialItems.WardrobeItems)
        {
            WarehouseItems = consumedMaterialItems.WarehouseItems,
        };
        var failed = session with { PlayerItems = failedItems };
        context.Sessions?.Set(context.Connection, failed);

        foreach (var materialFrame in materialFrames)
        {
            await context.Connection.SendAsync(materialFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(starDrill).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage create socket item " +
                $"{FormatSession(context with { Session = failed })} itemUid={target.Uid} itemId={target.ItemId} " +
                $"materialItemId={LegacyToughDrillItemId} count=1 roll={roll} forcedRoll={forcedRoll} " +
                $"result=failure returnedItemId={LegacyStarDrillItemId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyStarDrillCreateSocketItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            PlayerItemSnapshotItem target,
            bool targetIsEquipment,
            IReadOnlyList<uint> args,
            CancellationToken cancellationToken)
    {
        if (target.SocketTwo != LegacyNoSocket)
        {
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage star drill create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId} socketTwo={target.SocketTwo}; " +
                    "legacy requires no second socket.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryConsumeLegacySocketMaterials(
                session.PlayerItems,
                args,
                LegacyStarDrillItemId,
                count: 7,
                out var consumedMaterialItems,
                out var materialFrames))
        {
            await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: false, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage star drill create socket item " +
                    $"{FormatSession(context)} itemUid={target.Uid} itemId={target.ItemId}; " +
                    $"seven materials {LegacyStarDrillItemId} were not present in item usage args.")
                .ConfigureAwait(false);
            return;
        }

        var updatedTarget = CopyLegacyItemWithSocketTwo(target, LegacyEmptySocket);
        var updatedItems = ReplaceLegacySocketTarget(
            consumedMaterialItems,
            target,
            updatedTarget,
            targetIsEquipment);
        var updated = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updated);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(updatedTarget, mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var materialFrame in materialFrames)
        {
            await context.Connection.SendAsync(materialFrame, cancellationToken).ConfigureAwait(false);
        }

        await SendLegacyCreateSocketItemResponseAsync(context, target.Uid, success: true, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage create socket item " +
                $"{FormatSession(context with { Session = updated })} itemUid={target.Uid} itemId={target.ItemId} " +
                $"materialItemId={LegacyStarDrillItemId} count=7 result=success.")
            .ConfigureAwait(false);
    }

}
