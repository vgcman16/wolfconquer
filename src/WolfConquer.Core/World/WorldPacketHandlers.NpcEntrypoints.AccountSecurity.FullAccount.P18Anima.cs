using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyFullAccountP18AnimaItemId = 4_200_018;
    private const string LegacyFullAccountP18AnimaFullBagMessage =
        "Please make 1 more space in your inventory.";

    private static async ValueTask ApplyLegacyAccountSecurityFullAccountP18AnimaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const byte optionId = 17;
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox(LegacyFullAccountP18AnimaFullBagMessage)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountSecurity FullAccount P18 Anima " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
                    $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                    "reason=inventory-full; source bag check precedes item metadata and persistence.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityFullAccountP18AnimaFenceAsync(
                    context,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, LegacyFullAccountP18AnimaItemId, out var itemType))
        {
            await LogLegacyAccountSecurityFullAccountP18AnimaFenceAsync(
                    context,
                    context.WorldData is null ? "world-data-missing" : "itemtype-missing")
                .ConfigureAwait(false);
            return;
        }

        if (itemType.Id != LegacyFullAccountP18AnimaItemId ||
            itemType.LegacyDurability > ushort.MaxValue)
        {
            await LogLegacyAccountSecurityFullAccountP18AnimaFenceAsync(
                    context,
                    "itemtype-invalid")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem grantedItem;
        try
        {
            grantedItem = CreatePurchasedItems(
                    session.PlayerItems,
                    itemType,
                    count: 1,
                    isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            await LogLegacyAccountSecurityFullAccountP18AnimaFenceAsync(
                    context,
                    "item-uid-exhausted")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems
                .Concat([grantedItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        var itemFrame = LegacyGameItemPacket
            .FromSnapshotItem(grantedItem, mode: LegacyGameItemPacket.AddItemMode)
            .ToFrameBytes();

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                persistItems: true,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityFullAccountP18AnimaFenceAsync(
                    context,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount P18 Anima " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"itemId={LegacyFullAccountP18AnimaItemId} itemUid={grantedItem.Uid} " +
                $"durability={grantedItem.Durability} color={grantedItem.Color} bound=false " +
                "packetOrder=GameItemAdd.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyAccountSecurityFullAccountP18AnimaFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount P18 Anima " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId=17 " +
            $"itemId={LegacyFullAccountP18AnimaItemId} reason={reason}; " +
            "no item, session state, or packet mutation was committed.");
    }
}
