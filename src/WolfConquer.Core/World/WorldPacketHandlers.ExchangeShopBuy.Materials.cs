using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyExchangeShopBuy)]
    private static async ValueTask HandleLegacyExchangeShopBuyAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyExchangeShopBuyPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyExchangeShopBuy packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} npcId={packet.NpcID} shopId={packet.ShopID} " +
                    $"count={packet.Count} index={packet.Index}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == LegacyExchangeShopBuyType.GoodsEx)
        {
            await HandleLegacyExchangeShopBuyGoodsExAsync(
                    context,
                    session,
                    packet,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type != LegacyExchangeShopBuyType.Goods)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} npcId={packet.NpcID} shopId={packet.ShopID} " +
                    $"count={packet.Count} index={packet.Index}; action is not modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} index={packet.Index}; world data was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Count == 0 || packet.Count > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} index={packet.Index} count={packet.Count}; count is outside legacy bounds.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetLegacyExchangeShopGoodsRow(worldData.ExchangeShopGoods, packet.Index, out var goods))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} index={packet.Index}; exchange_shop_goods row was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (goods.ShopId != packet.ShopID)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} index={packet.Index} requestedShopId={packet.ShopID} rowShopId={goods.ShopId}.")
                .ConfigureAwait(false);
            return;
        }

        var purchasedCount = GetLegacyExchangeShopBuyCount(session, packet.Index);
        if (goods.Amount != 0 && (purchasedCount >= goods.Amount || packet.Count > goods.Amount - purchasedCount))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} source={LegacyExchangeShopBuyGoodsSourceMethod} type={packet.Type} index={packet.Index} " +
                    $"count={packet.Count} purchasedCount={purchasedCount} amountLimit={goods.Amount}.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} index={packet.Index}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadItemType(worldData, goods.ItemTypeId, out var rewardType))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} index={packet.Index} itemId={goods.ItemTypeId}; itemtype was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryPrepareLegacyExchangeMaterials(session.PlayerItems, goods.Materials, packet.Count,
                out var updatedItems, out var consumeFrames, out var requirementCount, out var materialReason))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyExchangeShopBuy {FormatSession(context)} " +
                    $"type={packet.Type} index={packet.Index} reason={materialReason}; no purchase material plan was accepted.")
                .ConfigureAwait(false);
            return;
        }

        var reward = CreatePurchasedItems(updatedItems, rewardType, 1, isBound: goods.Monopoly != 0)[0]
            .WithStackSize(checked((ushort)packet.Count));
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([reward]).ToArray(),
        };
        var updatedSession = session with
        {
            PlayerItems = updatedItems,
            LegacyExchangeShopBuyCounts = AddLegacyExchangeShopBuyCount(
                session.LegacyExchangeShopBuyCounts,
                packet.Index,
                packet.Count),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var consumeFrame in consumeFrames)
        {
            await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await PersistLegacyExchangeShopBuyItemsAsync(
                updatedContext,
                updatedSession,
                packet,
                goods.ItemTypeId,
                LegacyExchangeShopBuyGoodsSourceMethod)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyExchangeShopBuy " +
                $"{FormatSession(updatedContext)} source={LegacyExchangeShopBuyGoodsSourceMethod} type={packet.Type} npcId={packet.NpcID} " +
                $"shopId={packet.ShopID} index={packet.Index} count={packet.Count} purchasedCount={purchasedCount + packet.Count} " +
                $"amountLimit={goods.Amount} itemId={goods.ItemTypeId} materials={requirementCount}.")
            .ConfigureAwait(false);
    }
}
