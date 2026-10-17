using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyHonorShopId = 6_000;

    private const uint LegacyRacePointShopId = 6_001;

    private const uint LegacyChampionShopId = 6_003;

    private const int LegacyChampionShopBlockedItemId = 3_005_360;

    private const int LegacyGoldShopMoneyType = 0;

    private const int LegacyConquerPointsShopMoneyType = 1;

    private static readonly HashSet<int> LegacyBuyItemFromForgingItemTypeIds =
    [
        1_088_001,
        1_088_000,
        730_001,
        730_003,
        730_006,
        3_321_806,
        3_321_805,
        723_694,
        723_695,
        700_073,
        1_200_005,
    ];

    private static async ValueTask HandleBuyItemAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (packet.Id == 0 || packet.DwParam > int.MaxValue || packet.DwParam2 > LegacyMaxBuyCount)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage buy " +
                    $"{FormatSession(context)} shopId={packet.Id} itemId={packet.DwParam} count={packet.DwParam2}; request is outside legacy bounds.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Id == LegacyChampionShopId)
        {
            await HandleChampionShopBuyAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Id == LegacyRacePointShopId)
        {
            await HandleRacePointShopBuyAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Id == LegacyHonorShopId)
        {
            await HandleHonorShopBuyAsync(context, session, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!CanSeeBuyShop(context, session.Character, packet.Id))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage buy " +
                    $"{FormatSession(context)} shopId={packet.Id}; visible shop NPC was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData ||
            !worldData.ShopsById.TryGetValue((int)packet.Id, out var shops) ||
            shops.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage buy " +
                    $"{FormatSession(context)} shopId={packet.Id}; shop data was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var itemTypeId = (int)packet.DwParam;
        if (!worldData.ItemsById.TryGetValue(itemTypeId, out var itemTypes) || itemTypes.Count == 0)
        {
            if (packet.Id == LegacyRemoteShopId)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer deferred MsgItemUsage buy " +
                        $"{FormatSession(context)} shopId={packet.Id} itemId={itemTypeId}; remote mall client-only itemtype is not modeled.")
                    .ConfigureAwait(false);
            }
            else
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage buy " +
                        $"{FormatSession(context)} itemId={itemTypeId}; itemtype was not loaded.")
                    .ConfigureAwait(false);
            }

            return;
        }

        var itemType = itemTypes[0];
        var isBoundBuy = packet.DwParam3 == 2;
        var itemIsListedInShop = worldData.ShopItemsByShopId.TryGetValue((int)packet.Id, out var shopItems) &&
            shopItems.Any(item => item.ItemTypeId == itemTypeId && item.IsBound == isBoundBuy);
        var remoteMallItemFallback = packet.Id == LegacyRemoteShopId &&
            itemType.ConquerPointsWorth > 0;
        if (!itemIsListedInShop && !remoteMallItemFallback)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage buy " +
                    $"{FormatSession(context)} shopId={packet.Id} itemId={itemTypeId} bound={isBoundBuy}; item is not in the shop.")
                .ConfigureAwait(false);
            return;
        }

        var count = packet.DwParam2 == 0 ? 1 : checked((int)packet.DwParam2);
        var shop = shops[0];
        var sendMoneyUpdate = false;
        CharacterSnapshot updatedCharacter;
        long totalCost;
        switch (shop.MoneyType)
        {
            case LegacyGoldShopMoneyType:
                {
                    var itemGoldWorth = checked((long)itemType.GoldWorth);
                    totalCost = checked(itemGoldWorth * count);
                    if (itemGoldWorth < 1 || session.Character.Money < totalCost)
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer skipped MsgItemUsage buy " +
                                $"{FormatSession(context)} itemId={itemTypeId} cost={totalCost} money={session.Character.Money}.")
                            .ConfigureAwait(false);
                        return;
                    }

                    updatedCharacter = session.Character with { Money = session.Character.Money - totalCost };
                    sendMoneyUpdate = true;
                    break;
                }

            case LegacyConquerPointsShopMoneyType:
                {
                    var itemConquerPointsWorth = checked((long)itemType.ConquerPointsWorth);
                    totalCost = checked(itemConquerPointsWorth * count);
                    var availableConquerPoints = isBoundBuy
                        ? session.Character.BoundConquerPoints
                        : session.Character.ConquerPoints;
                    if (itemConquerPointsWorth < 1 || availableConquerPoints < totalCost)
                    {
                        await context.Log.WriteLineAsync(
                                "WorldServer skipped MsgItemUsage buy " +
                                $"{FormatSession(context)} itemId={itemTypeId} cost={totalCost} " +
                                $"{(isBoundBuy ? "boundConquerPoints" : "conquerPoints")}={availableConquerPoints}.")
                            .ConfigureAwait(false);
                        return;
                    }

                    updatedCharacter = isBoundBuy
                        ? session.Character with { BoundConquerPoints = session.Character.BoundConquerPoints - totalCost }
                        : session.Character with { ConquerPoints = session.Character.ConquerPoints - totalCost };
                    break;
                }

            default:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage buy " +
                        $"{FormatSession(context)} shopId={packet.Id} moneyType={shop.MoneyType}; only gold and CP shops are currently modeled.")
                    .ConfigureAwait(false);
                return;
        }

        var purchased = CreatePurchasedItems(session.PlayerItems, itemType, count, isBoundBuy);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(purchased).ToArray(),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in purchased)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (sendMoneyUpdate)
        {
            await SendWalletMoneyUpdateAsync(context, updatedSession.Character, cancellationToken).ConfigureAwait(false);
        }

        var characterValues = shop.MoneyType == LegacyGoldShopMoneyType
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Money"] = updatedSession.Character.Money.ToString(CultureInfo.InvariantCulture),
            }
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [isBoundBuy ? "BoundConquerPoints" : "ConquerPoints"] = (isBoundBuy
                    ? updatedSession.Character.BoundConquerPoints
                    : updatedSession.Character.ConquerPoints).ToString(CultureInfo.InvariantCulture),
            };
        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updatedSession,
                "buy",
                characterValues,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage buy " +
                $"{FormatSession(context)} shopId={packet.Id} itemId={itemTypeId} count={count} cost={totalCost} " +
                $"moneyType={shop.MoneyType} bound={isBoundBuy} remoteMallFallback={remoteMallItemFallback}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleBuyItemFromForgingAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (packet.DwParam > int.MaxValue || packet.DwParam2 > int.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage forging buy " +
                    $"{FormatSession(context)} itemId={packet.DwParam} count={packet.DwParam2}; request is outside legacy bounds.")
                .ConfigureAwait(false);
            return;
        }

        var itemTypeId = (int)packet.DwParam;
        var count = (int)packet.DwParam2;
        if (count <= 0 || !LegacyBuyItemFromForgingItemTypeIds.Contains(itemTypeId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage forging buy " +
                    $"{FormatSession(context)} shopOrItemId={packet.Id} itemId={itemTypeId} count={count}; " +
                    "item is not in MsgItemUsuagePacket.BuyItemFromForging allowlist.")
                .ConfigureAwait(false);
            return;
        }

        if (session.LegacyTradeActive)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage forging buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} count={count}; reason=inTrade.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, count))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage forging buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} count={count} " +
                    $"loadedInventoryItems={session.PlayerItems.InventoryItems.Count}; inventory is full.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData ||
            !worldData.ItemsById.TryGetValue(itemTypeId, out var itemTypes) ||
            itemTypes.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage forging buy " +
                    $"{FormatSession(context)} itemId={itemTypeId}; itemtype was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var itemType = itemTypes[0];
        var itemConquerPointsWorth = checked((long)itemType.ConquerPointsWorth);
        var availableConquerPoints = Math.Max(0, session.Character.ConquerPoints);
        var affordableCount = itemConquerPointsWorth > 0
            ? availableConquerPoints / itemConquerPointsWorth
            : 0;
        var purchasedCount = itemConquerPointsWorth > 0
            ? checked((int)Math.Min(count, affordableCount))
            : 0;
        if (purchasedCount <= 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage forging buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} cost={itemConquerPointsWorth} " +
                    $"conquerPoints={session.Character.ConquerPoints}.")
                .ConfigureAwait(false);
            return;
        }

        var plusRemainder = itemTypeId % 730_000;
        var plus = plusRemainder <= 9 ? checked((byte)plusRemainder) : (byte)0;
        var totalCost = checked(itemConquerPointsWorth * purchasedCount);
        var purchased = CreatePurchasedItems(session.PlayerItems, itemType, purchasedCount, plus: plus);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(purchased).ToArray(),
        };
        var updatedSession = session with
        {
            Character = session.Character with { ConquerPoints = session.Character.ConquerPoints - totalCost },
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in purchased)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updatedSession,
                "forging buy",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = updatedSession.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                persistItems: true)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage forging buy " +
                $"{FormatSession(context)} shopOrItemId={packet.Id} itemId={itemTypeId} count={purchasedCount} " +
                $"requestedCount={count} cost={totalCost} plus={plus} " +
                "source=MsgItemUsuagePacket.BuyItemFromForging.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleRacePointShopBuyAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage race-point buy " +
                    $"{FormatSession(context)}; world data was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.DwParam > int.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage race-point buy " +
                    $"{FormatSession(context)} itemId={packet.DwParam}; item is outside legacy bounds.")
                .ConfigureAwait(false);
            return;
        }

        var itemTypeId = (int)packet.DwParam;
        if (!worldData.RacePointShopItemsByItemTypeId.TryGetValue(itemTypeId, out var shopItems) ||
            shopItems.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage race-point buy " +
                    $"{FormatSession(context)} itemId={packet.DwParam}; item is not in RacePointShop.")
                .ConfigureAwait(false);
            return;
        }

        if (!worldData.ItemsById.TryGetValue(itemTypeId, out var itemTypes) || itemTypes.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage race-point buy " +
                    $"{FormatSession(context)} itemId={itemTypeId}; itemtype was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var shopItem = shopItems[0];
        var racePoints = ReadRacePoints(session.Character);
        if (shopItem.Cost < 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage race-point buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} cost={shopItem.Cost} racePoints={racePoints}.")
                .ConfigureAwait(false);
            return;
        }

        var cost = (ulong)shopItem.Cost;
        if (racePoints < cost)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage race-point buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} cost={cost} racePoints={racePoints}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedRacePoints = racePoints - cost;
        var purchased = CreatePurchasedItems(session.PlayerItems, itemTypes[0], 1);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(purchased).ToArray(),
        };
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(session.Character, "RacePoints", updatedRacePoints),
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        await SendScalarUpdateAsync(
                context,
                updatedSession.Character,
                LegacyMsgUpdateDataType.RaceShopPoints,
                updatedRacePoints,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var item in purchased)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updatedSession,
                "race-point buy",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["RacePoints"] = updatedRacePoints.ToString(CultureInfo.InvariantCulture),
                },
                persistItems: true)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage race-point buy " +
                $"{FormatSession(context)} itemId={itemTypeId} cost={cost} racePoints={updatedRacePoints}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleHonorShopBuyAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage honor buy " +
                    $"{FormatSession(context)}; world data was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.DwParam > int.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage honor buy " +
                    $"{FormatSession(context)} itemId={packet.DwParam}; item is outside legacy bounds.")
                .ConfigureAwait(false);
            return;
        }

        var itemTypeId = (int)packet.DwParam;
        if (!worldData.HonorShopItemsByItemTypeId.TryGetValue(itemTypeId, out var shopItems) ||
            shopItems.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage honor buy " +
                    $"{FormatSession(context)} itemId={packet.DwParam}; item is not in HonorShop.")
                .ConfigureAwait(false);
            return;
        }

        if (!worldData.ItemsById.TryGetValue(itemTypeId, out var itemTypes) || itemTypes.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage honor buy " +
                    $"{FormatSession(context)} itemId={itemTypeId}; itemtype was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var shopItem = shopItems[0];
        var arenaStatistics = GetArenaStatisticsForSession(context, session);
        if (shopItem.Cost < 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage honor buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} cost={shopItem.Cost} currentHonor={arenaStatistics.CurrentHonor}.")
                .ConfigureAwait(false);
            return;
        }

        var cost = checked((uint)shopItem.Cost);
        if (arenaStatistics.CurrentHonor < cost)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage honor buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} cost={cost} currentHonor={arenaStatistics.CurrentHonor}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCurrentHonor = arenaStatistics.CurrentHonor - cost;
        var updatedStatistics = arenaStatistics with { CurrentHonor = updatedCurrentHonor };
        var purchased = CreatePurchasedItems(session.PlayerItems, itemTypes[0], 1);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(purchased).ToArray(),
        };
        var updatedSession = session with
        {
            Character = WithArenaCurrentHonor(session.Character, updatedCurrentHonor),
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in purchased)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var status = context.ArenaStates?.GetStatus(context.Connection) ?? MsgArenaInfoStatus.NotSignedUp;
        await SendArenaInfoAsync(context, updatedStatistics, status, cancellationToken).ConfigureAwait(false);

        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updatedSession,
                "honor buy",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [RuntimeArenaCurrentHonorAttribute] = updatedCurrentHonor.ToString(CultureInfo.InvariantCulture),
                },
                persistItems: true)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage honor buy " +
                $"{FormatSession(context)} itemId={itemTypeId} cost={cost} currentHonor={updatedCurrentHonor}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleChampionShopBuyAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            MsgItemUsagePacket packet,
            CancellationToken cancellationToken)
    {
        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage champion buy " +
                    $"{FormatSession(context)}; world data was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.DwParam > int.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage champion buy " +
                    $"{FormatSession(context)} itemId={packet.DwParam}; item is outside legacy bounds.")
                .ConfigureAwait(false);
            return;
        }

        var itemTypeId = (int)packet.DwParam;
        var championPoints = ReadChampionPoints(session.Character);
        if (itemTypeId == LegacyChampionShopBlockedItemId)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage champion buy " +
                    $"{FormatSession(context)} itemId={itemTypeId}; legacy shop blocks this item.")
                .ConfigureAwait(false);
            return;
        }

        if (!worldData.GoldenLeagueShopItemsByItemTypeId.TryGetValue(itemTypeId, out var shopItems) ||
            shopItems.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage champion buy " +
                    $"{FormatSession(context)} itemId={packet.DwParam}; item is not in GoldenLeagueShop.")
                .ConfigureAwait(false);
            return;
        }

        if (!worldData.ItemsById.TryGetValue(itemTypeId, out var itemTypes) || itemTypes.Count == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage champion buy " +
                    $"{FormatSession(context)} itemId={itemTypeId}; itemtype was not loaded.")
                .ConfigureAwait(false);
            return;
        }

        var shopItem = shopItems[0];
        if (shopItem.Cost < 1 || championPoints > uint.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage champion buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} cost={shopItem.Cost} championPoints={championPoints}.")
                .ConfigureAwait(false);
            return;
        }

        var cost = (ulong)shopItem.Cost;
        if (championPoints <= cost)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage champion buy " +
                    $"{FormatSession(context)} itemId={itemTypeId} cost={cost} championPoints={championPoints}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedChampionPoints = championPoints - cost;
        var plus = itemTypeId is >= 730_001 and <= 730_008
            ? checked((byte)(itemTypeId % 10))
            : (byte)0;
        var purchased = CreatePurchasedItems(session.PlayerItems, itemTypes[0], 1, isBound: true, plus: plus);
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(purchased).ToArray(),
        };
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(session.Character, "ChampionPoints", updatedChampionPoints),
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        foreach (var item in purchased)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new MsgGoldLeaguePointPacket(checked((uint)updatedChampionPoints), HistoryPoints: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await TryPersistLegacyItemUsageMutationAsync(
                context,
                updatedSession,
                "champion buy",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ChampionPoints"] = updatedChampionPoints.ToString(CultureInfo.InvariantCulture),
                },
                persistItems: true)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage champion buy " +
                $"{FormatSession(context)} itemId={itemTypeId} cost={cost} championPoints={updatedChampionPoints}.")
            .ConfigureAwait(false);
    }
}
