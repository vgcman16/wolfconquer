using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyExchangeShopGoodsMinimumFieldCount = 13;
    private const int LegacyExchangeShopGoodsExMinimumFieldCount = 11;
    private const int LegacyExchangeShopGoodsExRefreshCount = 8;
    private const string LegacyExchangeShopBuyGoodsSourceMethod = "MrRayzo.ElTabakh.CMsgExchangeShopBuy.Process1.Type0";
    private const string LegacyExchangeShopBuyGoodsExSourceMethod = "MrRayzo.ElTabakh.CMsgExchangeShopBuy.Process1.Type1";
    private const string LegacyExchangeShopGoodsInfoSourceMethod = "MrRayzo.ElTabakh.CMsgExchangeShopGoodsInfo.Process2";
    private const uint LegacyExchangeShopGoodsExDonationCostType = 4;
    private const ulong LegacyExchangeShopGoodsExRefreshCost = 50;

    [PacketHandler(WorldPacketOpcodes.LegacyExchangeShopGoodsInfo)]
    private static async ValueTask HandleLegacyExchangeShopGoodsInfoAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyExchangeShopGoodsInfoPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyExchangeShopGoodsInfo packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopGoodsInfo " +
                    $"{FormatSession(context)} type={packet.Type} npcId={packet.NpcID} shopId={packet.ShopID}; " +
                    "no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type is not LegacyExchangeShopGoodsInfoType.Refresh and
            not LegacyExchangeShopGoodsInfoType.Show and
            not LegacyExchangeShopGoodsInfoType.Open)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopGoodsInfo " +
                    $"{FormatSession(context)} type={packet.Type} npcId={packet.NpcID} shopId={packet.ShopID}; " +
                    "action is not modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Type == LegacyExchangeShopGoodsInfoType.Refresh &&
            packet.ShopID != LegacyExchangeShopRandomShopId)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopGoodsInfo " +
                    $"{FormatSession(context)} type={packet.Type} npcId={packet.NpcID} shopId={packet.ShopID}; " +
                    "only the legacy random exchange shop is modeled.")
                .ConfigureAwait(false);
            return;
        }

        var activeSession = session;
        var activeContext = context;
        if (context.WorldData is { } worldData)
        {
            var goodsExCounts = packet.Type == LegacyExchangeShopGoodsInfoType.Refresh
                ? CreateLegacyExchangeShopGoodsExSelection(
                    worldData.ExchangeShopGoodsEx,
                    LegacyExchangeShopRandomShopId,
                    LegacyExchangeShopGoodsExRefreshCount)
                : activeSession.LegacyExchangeShopGoodsExCounts ??
                    CreateLegacyExchangeShopGoodsExSelection(
                        worldData.ExchangeShopGoodsEx,
                        LegacyExchangeShopRandomShopId,
                        LegacyExchangeShopGoodsExRefreshCount);

            if (packet.Type == LegacyExchangeShopGoodsInfoType.Refresh)
            {
                var guildDonation = ReadAttributeUInt64(activeSession.Character.Attributes, "GuildDontion", "GuildDonation", "MyDontion");
                if (guildDonation < LegacyExchangeShopGoodsExRefreshCost)
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped LegacyExchangeShopGoodsInfo " +
                            $"{FormatSession(context)} source={LegacyExchangeShopGoodsInfoSourceMethod} type={packet.Type} " +
                            $"npcId={packet.NpcID} shopId={packet.ShopID} cost={LegacyExchangeShopGoodsExRefreshCost} guildDonation={guildDonation}.")
                        .ConfigureAwait(false);
                    return;
                }

                var updatedGuildDonation = guildDonation - LegacyExchangeShopGoodsExRefreshCost;
                var updatedCharacter = WithLegacyGuildDonationAttributes(activeSession.Character, updatedGuildDonation);
                activeSession = activeSession with
                {
                    Character = updatedCharacter,
                    LegacyExchangeShopGoodsExCounts = goodsExCounts,
                };
                context.Sessions?.Set(context.Connection, activeSession);
                activeContext = context with { Session = activeSession };
            }
            else if (!ReferenceEquals(goodsExCounts, activeSession.LegacyExchangeShopGoodsExCounts))
            {
                activeSession = activeSession with { LegacyExchangeShopGoodsExCounts = goodsExCounts };
                context.Sessions?.Set(context.Connection, activeSession);
                activeContext = context with { Session = activeSession };
            }
        }

        var response = await SendLegacyExchangeShopGoodsExAsync(
                activeContext,
                activeSession,
                packet.NpcID,
                LegacyExchangeShopRandomShopId,
                LegacyExchangeShopGoodsInfoSourceMethod,
                cancellationToken)
            .ConfigureAwait(false);

        if (packet.Type == LegacyExchangeShopGoodsInfoType.Refresh &&
            !ReferenceEquals(activeSession, session))
        {
            var updatedGuildDonation = ReadAttributeUInt64(activeSession.Character.Attributes, "GuildDontion", "GuildDonation", "MyDontion");
            await SendScalarUpdateAsync(
                    activeContext,
                    activeSession.Character,
                    LegacyMsgUpdateDataType.MyDontion,
                    updatedGuildDonation,
                    cancellationToken)
                .ConfigureAwait(false);
            await SendScalarUpdateAsync(
                    activeContext,
                    activeSession.Character,
                    LegacyMsgUpdateDataType.DonationPoints,
                    updatedGuildDonation,
                    cancellationToken)
                .ConfigureAwait(false);
            await PersistLegacyExchangeShopDonationAsync(
                    activeContext,
                    activeSession,
                    "LegacyExchangeShopGoodsInfo GoodsEx refresh",
                    LegacyExchangeShopGoodsInfoSourceMethod)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyExchangeShopGoodsInfo " +
                $"{FormatSession(activeContext)} source={LegacyExchangeShopGoodsInfoSourceMethod} type={packet.Type} npcId={packet.NpcID} " +
                $"requestedShopId={packet.ShopID} shopId={response.ShopID} items={response.Items.Count}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<LegacyExchangeShopGoodsPacket> SendLegacyExchangeShopGoodsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        uint shopId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var response = CreateLegacyExchangeShopGoodsPacket(
            context.WorldData?.ExchangeShopGoods,
            npcId,
            shopId,
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            GetLegacyExchangeShopSecondsRemaining());

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyExchangeShopGoods " +
                $"{FormatSession(context)} source={sourceMethod} npcId={npcId} shopId={shopId} items={response.Items.Count}.")
            .ConfigureAwait(false);
        return response;
    }

    private static LegacyExchangeShopGoodsPacket CreateLegacyExchangeShopGoodsPacket(
        IReadOnlyList<ExchangeShopRow>? rows,
        uint npcId,
        uint shopId,
        uint uid,
        uint timeInSeconds)
    {
        var items = rows is null
            ? Array.Empty<LegacyExchangeShopGoodsItem>()
            : CreateLegacyExchangeShopGoodsItems(rows, shopId);

        return new LegacyExchangeShopGoodsPacket(npcId, shopId, uid, timeInSeconds, Unknown5: 0, items);
    }

    private static LegacyExchangeShopGoodsItem[] CreateLegacyExchangeShopGoodsItems(
        IReadOnlyList<ExchangeShopRow> rows,
        uint shopId)
    {
        var items = new List<LegacyExchangeShopGoodsItem>();
        foreach (var row in rows)
        {
            if (!TryParseLegacyExchangeShopGoodsRow(row, out var definition) || definition.ShopId != shopId)
            {
                continue;
            }

            items.Add(new LegacyExchangeShopGoodsItem(definition.Id, definition.Amount, Unknown: 0));
        }

        return items.ToArray();
    }

    private static async ValueTask<LegacyExchangeShopGoodsPacket> SendLegacyExchangeShopGoodsExAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        uint shopId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var response = CreateLegacyExchangeShopGoodsExPacket(
            context.WorldData?.ExchangeShopGoodsEx,
            session.LegacyExchangeShopGoodsExCounts,
            npcId,
            shopId,
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            GetLegacyExchangeShopSecondsRemaining());

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyExchangeShopGoods " +
                $"{FormatSession(context)} source={sourceMethod} npcId={npcId} shopId={shopId} items={response.Items.Count}.")
            .ConfigureAwait(false);
        return response;
    }

    private static LegacyExchangeShopGoodsPacket CreateLegacyExchangeShopGoodsExPacket(
        IReadOnlyList<ExchangeShopRow>? rows,
        IReadOnlyDictionary<uint, uint>? selectedCounts,
        uint npcId,
        uint shopId,
        uint uid,
        uint timeInSeconds)
    {
        var items = rows is null || selectedCounts is null
            ? Array.Empty<LegacyExchangeShopGoodsItem>()
            : CreateLegacyExchangeShopGoodsExItems(rows, selectedCounts, shopId);

        return new LegacyExchangeShopGoodsPacket(npcId, shopId, uid, timeInSeconds, Unknown5: 0, items);
    }

    private static LegacyExchangeShopGoodsItem[] CreateLegacyExchangeShopGoodsExItems(
        IReadOnlyList<ExchangeShopRow> rows,
        IReadOnlyDictionary<uint, uint> selectedCounts,
        uint shopId)
    {
        var items = new List<LegacyExchangeShopGoodsItem>();
        foreach (var row in rows)
        {
            if (!TryParseLegacyExchangeShopGoodsExRow(row, out var definition) ||
                definition.ShopId != shopId ||
                !selectedCounts.TryGetValue(definition.Id, out var count))
            {
                continue;
            }

            items.Add(new LegacyExchangeShopGoodsItem(definition.Id, count, Unknown: 0));
        }

        return items.ToArray();
    }

    private static IReadOnlyDictionary<uint, uint> CreateLegacyExchangeShopGoodsExSelection(
        IReadOnlyList<ExchangeShopRow> rows,
        uint shopId,
        int count)
    {
        if (count <= 0)
        {
            return new Dictionary<uint, uint>();
        }

        var candidates = new List<uint>();
        foreach (var row in rows)
        {
            if (TryParseLegacyExchangeShopGoodsExRow(row, out var definition) && definition.ShopId == shopId)
            {
                candidates.Add(definition.Id);
            }
        }

        var selected = new HashSet<uint>();
        var selectedCount = Math.Min(count, candidates.Count);
        for (var i = 0; i < selectedCount; i++)
        {
            var swapIndex = Random.Shared.Next(i, candidates.Count);
            (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            selected.Add(candidates[i]);
        }

        var counts = new Dictionary<uint, uint>();
        foreach (var candidate in candidates)
        {
            if (selected.Contains(candidate))
            {
                counts[candidate] = 0;
            }
        }

        return counts;
    }

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

        var requirements = goods.Materials
            .Where(material => material.ItemTypeId != 0 && material.Count != 0)
            .Select(material => material with { Count = checked(material.Count * packet.Count) })
            .ToArray();
        foreach (var requirement in requirements)
        {
            if (!HasLegacyInventoryStackCount(session.PlayerItems, requirement.ItemTypeId, requirement.Count))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyExchangeShopBuy " +
                        $"{FormatSession(context)} type={packet.Type} index={packet.Index} materialId={requirement.ItemTypeId} " +
                        $"required={requirement.Count}; material was not loaded in inventory.")
                    .ConfigureAwait(false);
                return;
            }
        }

        var updatedItems = session.PlayerItems;
        var consumeFrames = new List<byte[]>();
        foreach (var requirement in requirements)
        {
            updatedItems = ConsumeLegacyInventoryStack(
                updatedItems,
                requirement.ItemTypeId,
                requirement.Count,
                consumeFrames);
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
                $"amountLimit={goods.Amount} itemId={goods.ItemTypeId} materials={requirements.Length}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyExchangeShopBuyGoodsExAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyExchangeShopBuyPacket packet,
        CancellationToken cancellationToken)
    {
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

        if (!TryGetLegacyExchangeShopGoodsExRow(worldData.ExchangeShopGoodsEx, packet.Index, out var goods))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} type={packet.Type} index={packet.Index}; exchange_shop_goods_ex row was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var purchasedCount = GetLegacyExchangeShopGoodsExCount(session, packet.Index);
        if (goods.Amount != 0 && (purchasedCount >= goods.Amount || packet.Count > goods.Amount - purchasedCount))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} source={LegacyExchangeShopBuyGoodsExSourceMethod} type={packet.Type} index={packet.Index} " +
                    $"count={packet.Count} purchasedCount={purchasedCount} amountLimit={goods.Amount}.")
                .ConfigureAwait(false);
            return;
        }

        if (goods.CostType != LegacyExchangeShopGoodsExDonationCostType)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} source={LegacyExchangeShopBuyGoodsExSourceMethod} type={packet.Type} index={packet.Index} " +
                    $"costType={goods.CostType}; only legacy MyDontion cost_type=4 is modeled.")
                .ConfigureAwait(false);
            return;
        }

        var guildDonation = ReadAttributeUInt64(session.Character.Attributes, "GuildDontion", "GuildDonation", "MyDontion");
        if (guildDonation < goods.CostValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyExchangeShopBuy " +
                    $"{FormatSession(context)} source={LegacyExchangeShopBuyGoodsExSourceMethod} type={packet.Type} index={packet.Index} " +
                    $"cost={goods.CostValue} guildDonation={guildDonation}.")
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

        var updatedGuildDonation = guildDonation - goods.CostValue;
        var reward = CreatePurchasedItems(session.PlayerItems, rewardType, 1, isBound: goods.Monopoly != 0)[0]
            .WithStackSize(checked((ushort)packet.Count));
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([reward]).ToArray(),
        };
        var updatedCharacter = WithLegacyGuildDonationAttributes(session.Character, updatedGuildDonation);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
            LegacyExchangeShopGoodsExCounts = AddLegacyExchangeShopCount(
                session.LegacyExchangeShopGoodsExCounts,
                packet.Index,
                packet.Count),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(reward).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.MyDontion,
                updatedGuildDonation,
                cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.DonationPoints,
                updatedGuildDonation,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await PersistLegacyExchangeShopBuyItemsAsync(
                updatedContext,
                updatedSession,
                packet,
                goods.ItemTypeId,
                LegacyExchangeShopBuyGoodsExSourceMethod)
            .ConfigureAwait(false);
        await PersistLegacyExchangeShopDonationAsync(
                updatedContext,
                updatedSession,
                "LegacyExchangeShopBuy GoodsEx",
                LegacyExchangeShopBuyGoodsExSourceMethod)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyExchangeShopBuy " +
                $"{FormatSession(updatedContext)} source={LegacyExchangeShopBuyGoodsExSourceMethod} type={packet.Type} npcId={packet.NpcID} " +
                $"shopId={packet.ShopID} rowShopId={goods.ShopId} index={packet.Index} count={packet.Count} " +
                $"purchasedCount={purchasedCount + packet.Count} amountLimit={goods.Amount} itemId={goods.ItemTypeId} " +
                $"cost={goods.CostValue} guildDonation={updatedGuildDonation}.")
            .ConfigureAwait(false);
    }

    private static uint GetLegacyExchangeShopBuyCount(WorldSession session, uint goodsRecordId)
    {
        return session.LegacyExchangeShopBuyCounts is not null &&
            session.LegacyExchangeShopBuyCounts.TryGetValue(goodsRecordId, out var count)
            ? count
            : 0;
    }

    private static uint GetLegacyExchangeShopGoodsExCount(WorldSession session, uint goodsRecordId)
    {
        return session.LegacyExchangeShopGoodsExCounts is not null &&
            session.LegacyExchangeShopGoodsExCounts.TryGetValue(goodsRecordId, out var count)
            ? count
            : 0;
    }

    private static IReadOnlyDictionary<uint, uint> AddLegacyExchangeShopBuyCount(
        IReadOnlyDictionary<uint, uint>? counts,
        uint goodsRecordId,
        uint amount)
    {
        return AddLegacyExchangeShopCount(counts, goodsRecordId, amount);
    }

    private static IReadOnlyDictionary<uint, uint> AddLegacyExchangeShopCount(
        IReadOnlyDictionary<uint, uint>? counts,
        uint goodsRecordId,
        uint amount)
    {
        var updated = counts is null
            ? new Dictionary<uint, uint>()
            : new Dictionary<uint, uint>(counts);
        updated.TryGetValue(goodsRecordId, out var current);
        updated[goodsRecordId] = current > uint.MaxValue - amount
            ? uint.MaxValue
            : current + amount;
        return updated;
    }

    private static async ValueTask PersistLegacyExchangeShopBuyItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyExchangeShopBuyPacket packet,
        uint itemTypeId,
        string sourceMethod)
    {
        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    session.Character.Uid,
                    session.PlayerItems);
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyExchangeShopBuy items " +
                        $"{FormatSession(context)} source={sourceMethod} " +
                        $"path={session.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin " +
                        $"type={packet.Type} shopId={packet.ShopID} index={packet.Index} itemId={itemTypeId}.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyExchangeShopBuy item persistence " +
                        $"{FormatSession(context)} source={sourceMethod} " +
                        $"type={packet.Type} shopId={packet.ShopID} index={packet.Index} itemId={itemTypeId} error={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyExchangeShopBuy missing PlayerItemsRoot " +
                    $"{FormatSession(context)} source={sourceMethod} " +
                    $"type={packet.Type} shopId={packet.ShopID} index={packet.Index} itemId={itemTypeId}; inventory mutation is session-only.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask PersistLegacyExchangeShopDonationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string operation,
        string sourceMethod)
    {
        var donation = ReadAttributeUInt64(session.Character.Attributes, "GuildDontion", "GuildDonation", "MyDontion");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildDontion"] = donation.ToString(CultureInfo.InvariantCulture),
        };

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped {operation} donation persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                $"WorldServer persisted {operation} donation " +
                $"{FormatSession(context)} source={sourceMethod} " +
                $"path={Path.GetFileName(path)} keys=GuildDontion guildDonation={donation}.")
            .ConfigureAwait(false);
    }

    private static uint GetLegacyExchangeShopSecondsRemaining()
    {
        var now = DateTime.Now;
        var reset = now.Date.AddDays(1);
        var seconds = Math.Ceiling((reset - now).TotalSeconds);
        return seconds <= 0
            ? 0
            : (uint)Math.Min(seconds, 86_400);
    }

    private static bool TryGetLegacyExchangeShopGoodsRow(
        IReadOnlyList<ExchangeShopRow> rows,
        uint index,
        out LegacyExchangeShopGoodsDefinition definition)
    {
        foreach (var row in rows)
        {
            if (TryParseLegacyExchangeShopGoodsRow(row, out var parsed) && parsed.Id == index)
            {
                definition = parsed;
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static bool TryParseLegacyExchangeShopGoodsRow(
        ExchangeShopRow row,
        out LegacyExchangeShopGoodsDefinition definition)
    {
        definition = default;
        if (row.Fields.Count < LegacyExchangeShopGoodsMinimumFieldCount)
        {
            return false;
        }

        var fields = row.Fields;
        if (!TryParseLegacyExchangeUInt32(fields[0], out var id) ||
            !TryParseLegacyExchangeUInt32(fields[1], out var itemTypeId) ||
            !TryParseLegacyExchangeUInt32(fields[2], out var shopId) ||
            !TryParseLegacyExchangeUInt32(fields[3], out var amount) ||
            !TryParseLegacyExchangeUInt32(fields[4], out var monopoly) ||
            !TryParseLegacyExchangeMaterial(fields, 5, 6, out var material1) ||
            !TryParseLegacyExchangeMaterial(fields, 7, 8, out var material2) ||
            !TryParseLegacyExchangeMaterial(fields, 9, 10, out var material3) ||
            !TryParseLegacyExchangeMaterial(fields, 11, 12, out var material4))
        {
            return false;
        }

        definition = new LegacyExchangeShopGoodsDefinition(
            id,
            itemTypeId,
            shopId,
            amount,
            monopoly,
            [material1, material2, material3, material4]);
        return true;
    }

    private static bool TryGetLegacyExchangeShopGoodsExRow(
        IReadOnlyList<ExchangeShopRow> rows,
        uint index,
        out LegacyExchangeShopGoodsExDefinition definition)
    {
        foreach (var row in rows)
        {
            if (TryParseLegacyExchangeShopGoodsExRow(row, out var parsed) && parsed.Id == index)
            {
                definition = parsed;
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static bool TryParseLegacyExchangeShopGoodsExRow(
        ExchangeShopRow row,
        out LegacyExchangeShopGoodsExDefinition definition)
    {
        definition = default;
        if (row.Fields.Count < LegacyExchangeShopGoodsExMinimumFieldCount)
        {
            return false;
        }

        var fields = row.Fields;
        if (!TryParseLegacyExchangeUInt32(fields[0], out var id) ||
            !TryParseLegacyExchangeUInt32(fields[1], out var itemTypeId) ||
            !TryParseLegacyExchangeUInt32(fields[2], out var shopId) ||
            !TryParseLegacyExchangeUInt32(fields[3], out var amount) ||
            !TryParseLegacyExchangeUInt32(fields[4], out var monopoly) ||
            !TryParseLegacyExchangeUInt32(fields[5], out var costType) ||
            !TryParseLegacyExchangeUInt32(fields[6], out var costValue))
        {
            return false;
        }

        definition = new LegacyExchangeShopGoodsExDefinition(
            id,
            itemTypeId,
            shopId,
            amount,
            monopoly,
            costType,
            costValue);
        return true;
    }

    private static bool TryParseLegacyExchangeMaterial(
        IReadOnlyList<string> fields,
        int itemIndex,
        int countIndex,
        out LegacyExchangeShopMaterial material)
    {
        material = default;
        if (!TryParseLegacyExchangeUInt32(fields[itemIndex], out var itemTypeId) ||
            !TryParseLegacyExchangeUInt32(fields[countIndex], out var count))
        {
            return false;
        }

        material = new LegacyExchangeShopMaterial(itemTypeId, count);
        return true;
    }

    private static bool TryParseLegacyExchangeUInt32(string value, out uint parsed)
    {
        return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool HasLegacyInventoryStackCount(
        PlayerItemSnapshot items,
        uint itemTypeId,
        uint requiredCount)
    {
        var available = 0UL;
        foreach (var item in items.InventoryItems.Where(item => item.ItemId == itemTypeId))
        {
            available += item.StackSize == 0 ? 1UL : item.StackSize;
            if (available >= requiredCount)
            {
                return true;
            }
        }

        return false;
    }

    private static PlayerItemSnapshot ConsumeLegacyInventoryStack(
        PlayerItemSnapshot items,
        uint itemTypeId,
        uint count,
        List<byte[]> mutationFrames)
    {
        var remaining = count;
        var inventoryItems = items.InventoryItems.ToList();
        for (var index = 0; index < inventoryItems.Count && remaining > 0; index++)
        {
            var item = inventoryItems[index];
            if (item.ItemId != itemTypeId)
            {
                continue;
            }

            var available = item.StackSize == 0 ? 1U : item.StackSize;
            var consumed = Math.Min(available, remaining);
            if (consumed >= available)
            {
                mutationFrames.Add(
                    new MsgItemUsagePacket(
                        item.Uid,
                        DwParam: 0,
                        MsgItemUsageAction.RemoveInventory,
                        Timestamp: 0,
                        DwParam2: 0,
                        DwParam3: 0,
                        DwParam4: 0).ToFrameBytes());
                inventoryItems.RemoveAt(index);
                index--;
            }
            else
            {
                var updatedItem = item.WithStackSize(checked((ushort)(available - consumed)));
                inventoryItems[index] = updatedItem;
                mutationFrames.Add(
                    LegacyGameItemPacket.FromSnapshotItem(
                        updatedItem,
                        mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
            }

            remaining -= consumed;
        }

        return new PlayerItemSnapshot(
            inventoryItems,
            items.RelicFuseItems,
            items.MythsoulBagItems,
            items.EquipmentItems,
            items.RuneItems,
            items.WardrobeItems)
        {
            WarehouseItems = items.WarehouseItems,
        };
    }

    private readonly record struct LegacyExchangeShopGoodsDefinition(
        uint Id,
        uint ItemTypeId,
        uint ShopId,
        uint Amount,
        uint Monopoly,
        IReadOnlyList<LegacyExchangeShopMaterial> Materials);

    private readonly record struct LegacyExchangeShopGoodsExDefinition(
        uint Id,
        uint ItemTypeId,
        uint ShopId,
        uint Amount,
        uint Monopoly,
        uint CostType,
        uint CostValue);

    private readonly record struct LegacyExchangeShopMaterial(uint ItemTypeId, uint Count);
}
