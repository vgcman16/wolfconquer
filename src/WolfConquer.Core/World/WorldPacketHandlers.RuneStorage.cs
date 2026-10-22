using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyRuneBagPosition = 211;
    private const ushort LegacyRuneStoragePosition = 212;
    private const string LegacyRuneStorageSource = "MsgRuneStorage.Process";

    [PacketHandler(WorldPacketOpcodes.LegacyRuneStorage)]
    private static async ValueTask HandleLegacyRuneStorageAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyRuneStoragePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyRuneStorage packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyRuneStorage {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var ownerUid = session.Character.Uid;
        if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref session))
        {
            return;
        }

        if (session.Character.Reborn < 2)
        {
            if (packet.Type != LegacyRuneStoragePacket.StartupType)
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket
                            .SystemTalk("You need to be a second reborn hero to interact with rune system!")
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyRuneStorage " +
                    $"{FormatSession(context)} source={LegacyRuneStorageSource} type={packet.Type}; " +
                    "source requires second reborn for rune storage interactions.")
                .ConfigureAwait(false);
            return;
        }

        var beforeItems = session.PlayerItems;
        var updatedSession = session;
        var changed = false;
        var innerQueryRequired = false;
        switch (packet.Type)
        {
            case 0:
                if (TryGetRuneItem(session.PlayerItems, packet.ItemUID, out var runeToStore) &&
                    !HasRuneStorageSameBase(session.PlayerItems, runeToStore))
                {
                    updatedSession = session with
                    {
                        PlayerItems = ReplaceRuneItem(
                            session.PlayerItems,
                            runeToStore,
                            runeToStore.WithPosition(LegacyRuneStoragePosition)),
                    };
                    changed = true;
                }
                break;

            case 1:
                if (TryGetRuneItem(session.PlayerItems, packet.ItemUID, out var runeToBag))
                {
                    updatedSession = session with
                    {
                        PlayerItems = ReplaceRuneItem(
                            session.PlayerItems,
                            runeToBag,
                            runeToBag.WithPosition(LegacyRuneBagPosition)),
                    };
                    changed = true;
                }
                break;

            case LegacyRuneStoragePacket.StartupType:
                await context.Connection.SendAsync(
                        CreateLegacyRuneRankingMessage(context, session).ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                break;

            case 3:
                var inventoryRune = session.PlayerItems.InventoryItems.FirstOrDefault(item => item.Uid == packet.ItemUID);
                innerQueryRequired = inventoryRune is not null &&
                    !HasRuneStorageSameBase(session.PlayerItems, inventoryRune);
                if (TryMoveInventoryRuneToStorage(session.PlayerItems, packet.ItemUID, out var movedItems))
                {
                    updatedSession = session with { PlayerItems = movedItems };
                    changed = true;
                }
                break;

            default:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyRuneStorage unknown type " +
                        $"{FormatSession(context)} source={LegacyRuneStorageSource} type={packet.Type}.")
                    .ConfigureAwait(false);
                break;
        }

        if (!TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref session))
        {
            return;
        }

        var acceptedItems = updatedSession.PlayerItems;
        if (changed)
        {
            if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, beforeItems, ref session))
            {
                return;
            }

            updatedSession = PreserveLegacyPhysicalStatusForItemMutation(
                context, session, session with { PlayerItems = acceptedItems });
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };
            await TryPersistLegacyItemUsageMutationAsync(
                    updatedContext,
                    updatedSession,
                    "rune storage",
                    characterValues: null,
                    persistItems: true)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyEquipmentMutationSession(context, ownerUid, acceptedItems, ref updatedSession))
            {
                return;
            }
        }
        else
        {
            updatedSession = session;
        }

        // Source's inventory-found/no212-same-base branch queries even if Rune.Add
        // failed. This is distinct from the common post-response query below.
        if (innerQueryRequired)
        {
            var inner = await ExecuteLegacyEquipmentQueryAsync(
                    context with { Session = updatedSession }, updatedSession,
                    new LegacyEquipmentQueryRequest(
                        LegacyEquipmentRebuildReason.RuneStorageQuery,
                        new LegacyEquipmentQueryPublicationScope(ownerUid, changed ? acceptedItems : null)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!inner.Completed)
            {
                return;
            }

            updatedSession = inner.Session!;
            if (changed
                    ? !TryRefreshLegacyEquipmentMutationSession(context, ownerUid, acceptedItems, ref updatedSession)
                    : !TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref updatedSession))
            {
                return;
            }
        }

        var response = new LegacyRuneStoragePacket(
            packet.Type,
            packet.ItemUID,
            LegacyRuneStoragePacket.Startup(
                updatedSession.PlayerItems,
                context.WorldData?.RuneStorageAttributes).HPAdd);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (changed
                ? !TryRefreshLegacyEquipmentMutationSession(context, ownerUid, acceptedItems, ref updatedSession)
                : !TryRefreshLegacyPlayerMagicResourceSession(context, ownerUid, ref updatedSession))
        {
            return;
        }

        // MsgRuneStorage.Process queries after EVERY eligible processed response,
        // including Startup/read-only and unknown-type requests.
        var query = await ExecuteLegacyEquipmentQueryAsync(
                context with { Session = updatedSession }, updatedSession,
                new LegacyEquipmentQueryRequest(
                    LegacyEquipmentRebuildReason.RuneStorageQuery,
                    new LegacyEquipmentQueryPublicationScope(ownerUid, changed ? acceptedItems : null)),
                cancellationToken)
            .ConfigureAwait(false);
        if (!query.Completed)
        {
            return;
        }

        updatedSession = query.Session!;
        UpdateLegacyRuneRanking(context, updatedSession);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyRuneStorage " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyRuneStorageSource} " +
                $"type={packet.Type} itemUid={packet.ItemUID} changed={changed} hpAdd={response.HPAdd}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetRuneItem(
        PlayerItemSnapshot items,
        uint uid,
        out PlayerItemSnapshotItem rune)
    {
        rune = items.RuneItems.FirstOrDefault(item => item.Uid == uid)!;
        return rune is not null;
    }

    private static PlayerItemSnapshot ReplaceRuneItem(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem oldItem,
        PlayerItemSnapshotItem newItem)
    {
        return items with
        {
            RuneItems = items.RuneItems
                .Select(item => item.Uid == oldItem.Uid ? newItem : item)
                .OrderBy(item => item.Position)
                .ThenBy(item => IsLegacyRuneBagPosition(item.Position) ? 0u : item.Uid)
                .ToArray(),
        };
    }

    private static bool TryMoveInventoryRuneToStorage(
        PlayerItemSnapshot items,
        uint uid,
        out PlayerItemSnapshot updatedItems)
    {
        updatedItems = items;
        var inventoryItem = items.InventoryItems.FirstOrDefault(item => item.Uid == uid);
        if (inventoryItem is null ||
            HasRuneStorageSameBase(items, inventoryItem) ||
            items.RuneItems.Any(item => item.Uid == inventoryItem.Uid) ||
            items.RuneItems.Count(item => item.Position == LegacyRuneBagPosition) >= 100)
        {
            return false;
        }

        updatedItems = items with
        {
            InventoryItems = items.InventoryItems.Where(item => item.Uid != inventoryItem.Uid).ToArray(),
            RuneItems = items.RuneItems
                .Concat([inventoryItem.WithPosition(LegacyRuneStoragePosition)])
                .OrderBy(item => item.Position)
                .ThenBy(item => IsLegacyRuneBagPosition(item.Position) ? 0u : item.Uid)
                .ToArray(),
        };
        return true;
    }

    private static bool HasRuneStorageSameBase(
        PlayerItemSnapshot items,
        PlayerItemSnapshotItem item)
    {
        var itemBase = GetLegacyRuneStorageBaseItemId(item.ItemId);
        return items.RuneItems.Any(candidate =>
            candidate.Position == LegacyRuneStoragePosition &&
            GetLegacyRuneStorageBaseItemId(candidate.ItemId) == itemBase);
    }

    private static uint GetLegacyRuneStorageBaseItemId(uint itemId)
    {
        return itemId / 100;
    }

    private static bool IsLegacyRuneBagPosition(ushort position)
    {
        return position is LegacyRuneBagPosition or LegacyRuneStoragePosition;
    }
}
