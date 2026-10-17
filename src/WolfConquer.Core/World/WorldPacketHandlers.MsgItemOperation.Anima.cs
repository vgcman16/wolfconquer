using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyEmbedAnimaSource = "MsgItemUsuagePacket.EmbedAnima";
    private const string LegacyRemoveAnimaSource = "MsgItemUsuagePacket.RemoveAnima";

    private static async ValueTask HandleMsgItemOperationEmbedAnimaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemOperationPacket packet,
        CancellationToken cancellationToken)
    {
        if (await TryBlockLegacySecurityAnimaItemOperationAsync(
                context,
                session,
                LegacyEmbedAnimaSource,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindMsgItemOperationAnimaHost(
                session.PlayerItems,
                packet.HostItemUid,
                out var host,
                out var hostIsEquipped))
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "host-item-missing")
                .ConfigureAwait(false);
            return;
        }

        var selectedAnima = session.PlayerItems.InventoryItems
            .FirstOrDefault(item => item.Uid == packet.SecondaryItemUid);
        if (selectedAnima is null)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "anima-item-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!AllowsLegacyAnimaEmbed(host.Position))
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "host-position-disallowed")
                .ConfigureAwait(false);
            return;
        }

        var consumedAnima = session.PlayerItems.InventoryItems
            .FirstOrDefault(item => item.ItemId == selectedAnima.ItemId);
        if (consumedAnima is null)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "anima-item-type-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!hostIsEquipped && consumedAnima.Uid == host.Uid)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "host-matches-consumed-anima")
                .ConfigureAwait(false);
            return;
        }

        var updatedHost = host.WithAnimaItemId(selectedAnima.ItemId);
        var updatedItems = RemoveInventoryItem(session.PlayerItems, consumedAnima);
        updatedItems = hostIsEquipped
            ? ReplaceEquipmentItem(updatedItems, host, updatedHost)
            : ReplaceInventoryItem(updatedItems, host, updatedHost);
        var updatedSession = session with { PlayerItems = updatedItems };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistMsgItemOperationItems(context, updatedSession, out var persistenceReason))
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                new MsgItemUsagePacket(
                    consumedAnima.Uid,
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
                    .FromSnapshotItem(updatedHost, mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerEquipmentAppearanceRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer embedded Anima through active CMsgItemPB " +
                $"{FormatSession(updatedContext)} opcode={WorldPacketOpcodes.MsgItemOperation} " +
                $"action={(uint)packet.Action} hostUid={host.Uid} selectedAnimaUid={selectedAnima.Uid} " +
                $"consumedAnimaUid={consumedAnima.Uid} animaItemId={selectedAnima.ItemId} " +
                $"hostCollection={(hostIsEquipped ? "equipment" : "inventory")} timestampToken={packet.TimestampToken}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleMsgItemOperationRemoveAnimaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemOperationPacket packet,
        CancellationToken cancellationToken)
    {
        if (await TryBlockLegacySecurityAnimaItemOperationAsync(
                context,
                session,
                LegacyRemoveAnimaSource,
                cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (packet.SecondaryItemUid != 0)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "remove-parameter-not-zero")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindMsgItemOperationAnimaHost(
                session.PlayerItems,
                packet.HostItemUid,
                out var host,
                out var hostIsEquipped))
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "host-item-missing")
                .ConfigureAwait(false);
            return;
        }

        if (host.AnimaItemId == 0)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "host-anima-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "inventory-full")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, host.AnimaItemId, out var animaType) ||
            animaType.Id != host.AnimaItemId ||
            animaType.LegacyDurability > ushort.MaxValue)
        {
            await LogMsgItemOperationAnimaFenceAsync(
                    context,
                    packet,
                    context.WorldData is null ? "world-data-missing" : "anima-itemtype-missing-or-invalid")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem restoredAnima;
        try
        {
            restoredAnima = CreatePurchasedItems(
                    session.PlayerItems,
                    animaType,
                    count: 1,
                    isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, "item-uid-exhausted")
                .ConfigureAwait(false);
            return;
        }

        var updatedHost = host.WithAnimaItemId(0);
        var updatedItems = hostIsEquipped
            ? ReplaceEquipmentItem(session.PlayerItems, host, updatedHost)
            : ReplaceInventoryItem(session.PlayerItems, host, updatedHost);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems
                .Concat([restoredAnima])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistMsgItemOperationItems(context, updatedSession, out var persistenceReason))
        {
            await LogMsgItemOperationAnimaFenceAsync(context, packet, persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(restoredAnima, mode: LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(updatedHost, mode: LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyEquipmentRuntimeRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerEquipmentAppearanceRefreshAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer removed Anima through active CMsgItemPB " +
                $"{FormatSession(updatedContext)} opcode={WorldPacketOpcodes.MsgItemOperation} " +
                $"action={(uint)packet.Action} hostUid={host.Uid} restoredAnimaUid={restoredAnima.Uid} " +
                $"animaItemId={restoredAnima.ItemId} hostCollection={(hostIsEquipped ? "equipment" : "inventory")} " +
                $"timestampToken={packet.TimestampToken}.")
            .ConfigureAwait(false);
    }

    private static bool TryFindMsgItemOperationAnimaHost(
        PlayerItemSnapshot items,
        uint uid,
        out PlayerItemSnapshotItem host,
        out bool equipped)
    {
        var inventoryItem = items.InventoryItems.FirstOrDefault(item => item.Uid == uid);
        if (inventoryItem is not null)
        {
            host = inventoryItem;
            equipped = false;
            return true;
        }

        var equipmentItem = items.EquipmentItems.FirstOrDefault(item => item.Uid == uid);
        if (equipmentItem is not null)
        {
            host = equipmentItem;
            equipped = true;
            return true;
        }

        host = null!;
        equipped = false;
        return false;
    }

    private static bool AllowsLegacyAnimaEmbed(ushort position)
    {
        return position is not (7 or 9 or 10 or 11 or 12 or 13 or 15 or 16 or 17 or 18 or 19 or 57 or 59);
    }

    private static bool TryPersistMsgItemOperationItems(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            reason = "player-items-root-missing";
            return false;
        }

        var itemPath = Path.Combine(
            context.PlayerItemsRoot,
            updatedSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var tempPath = itemPath + ".msg-item-operation-" + transactionId + ".tmp";
        var backupPath = itemPath + ".msg-item-operation-" + transactionId + ".bak";
        var itemExisted = File.Exists(itemPath);

        try
        {
            Directory.CreateDirectory(context.PlayerItemsRoot);
            File.WriteAllBytes(tempPath, PlayerItemSnapshotLoader.ToLegacyBytes(updatedSession.PlayerItems));
            if (itemExisted)
            {
                File.Replace(tempPath, itemPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, itemPath);
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException)
        {
            var restored = TryRestoreMsgItemOperationItems(backupPath, itemPath, itemExisted);
            reason = restored
                ? "item-persistence-failed:" + ex.GetType().Name
                : "item-persistence-rollback-failed:" + ex.GetType().Name;
            return false;
        }
        finally
        {
            TryDeleteMsgItemOperationFile(tempPath);
        }

        TryDeleteMsgItemOperationFile(backupPath);
        return true;
    }

    private static bool TryRestoreMsgItemOperationItems(
        string backupPath,
        string itemPath,
        bool itemExisted)
    {
        try
        {
            if (File.Exists(backupPath))
            {
                if (File.Exists(itemPath))
                {
                    File.Replace(backupPath, itemPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(backupPath, itemPath);
                }

                return true;
            }

            if (!itemExisted && File.Exists(itemPath))
            {
                File.Delete(itemPath);
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            return false;
        }
    }

    private static void TryDeleteMsgItemOperationFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
        }
    }

    private static Task LogMsgItemOperationAnimaFenceAsync(
        WorldPacketHandlerContext context,
        MsgItemOperationPacket packet,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP MsgItemOperation Anima mutation fenced " +
            $"{FormatSession(context)} opcode={WorldPacketOpcodes.MsgItemOperation} action={(uint)packet.Action} " +
            $"hostUid={packet.HostItemUid} secondaryItemUid={packet.SecondaryItemUid} reason={reason}; " +
            "no item, session state, persistence, or packet mutation was committed.");
    }
}
