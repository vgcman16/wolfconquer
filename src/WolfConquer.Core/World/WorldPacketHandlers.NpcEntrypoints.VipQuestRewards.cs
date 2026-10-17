using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyArthurNpcId = 3_601;
    private const string LegacyArthurSourceMethod = "NpcHandler.Arthur";

    private const uint LegacyArthurMossItemId = 722_723;
    private const uint LegacyArthurSoulAromaItemId = 722_724;
    private const uint LegacyArthurDreamGrassItemId = 722_725;
    private const uint LegacyArthurGhostHornItemId = 722_722;
    private const uint LegacyArthurEvilToothItemId = 722_721;
    private const uint LegacyArthurImpureVigorItemId = 722_730;
    private const uint LegacyArthurVipTokenItemId = 780_001;

    private static readonly IReadOnlyList<string> LegacyArthurIntroTexts =
    [
        "This place can be quite dangerous. If you want to leave, just come talk to me.",
        "Well.. i can help you to get vip(Level 4) token. for 2 hours \n",
        "There are 4 stages for you to complete the Vip6 Quest: \n",
        "Stage 1. Change 3 Moss, 3 SoulAroma and 3 DreamGras for an GhostHorn. \n",
        "Stage 2. Change 3xGhostHorn for an EvilTooth. \n",
        "Stage 3. Change 4xEvilTooth for an ImpureVigor. \n",
        "Stage 4. Change 5xImpureVigor for an 1DayVIP token. \n",
    ];

    private static readonly IReadOnlyList<LegacyNpcDialogOption> LegacyArthurIntroOptions =
    [
        new("I have (Moss,SoulAroma,DreamGras)", 1),
        new("I have 3xGhostHorn to trade.", 2),
        new("I have 4xEvilTooth to trade.", 3),
        new("I have 5xImpureVigor to trade", 4),
        new("Teleport~to~Twin~City", 5),
        new("I`ll~think~it~over.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyArthurVipQuestAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyArthurNpcId)
        {
            return false;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyArthurSourceMethod,
                        LegacyArthurIntroTexts,
                        LegacyArthurIntroOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                return await TryExchangeLegacyArthurVipQuestItemsAsync(
                        context,
                        session,
                        optionId,
                        [
                            new LegacyArthurItemCost(LegacyArthurMossItemId, 3),
                            new LegacyArthurItemCost(LegacyArthurSoulAromaItemId, 3),
                            new LegacyArthurItemCost(LegacyArthurDreamGrassItemId, 3),
                        ],
                        LegacyArthurGhostHornItemId,
                        successText: "You successfully exchange the (Moss,SoulAroma and DreamGras) for an GhostHorn!",
                        missingText: "I'm sorry, you need at least 3xMoss, 3xSoulAroma and 3xDreamGras in order for me to help you.",
                        broadcastCompletion: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            case 2:
                return await TryExchangeLegacyArthurVipQuestItemsAsync(
                        context,
                        session,
                        optionId,
                        [new LegacyArthurItemCost(LegacyArthurGhostHornItemId, 3)],
                        LegacyArthurEvilToothItemId,
                        successText: "You successfully exchange the GhostHorn for an EvilTooth!",
                        missingText: "I'm sorry, you need at least 3 GhostHorn in order for me to help you.",
                        broadcastCompletion: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            case 3:
                return await TryExchangeLegacyArthurVipQuestItemsAsync(
                        context,
                        session,
                        optionId,
                        [new LegacyArthurItemCost(LegacyArthurEvilToothItemId, 4)],
                        LegacyArthurImpureVigorItemId,
                        successText: "You successfully exchange the EvilTooth for an ImpureVigor!",
                        missingText: "I'm sorry, you need at least 4 EvilTooth in order for me to help you.",
                        broadcastCompletion: false,
                        cancellationToken)
                    .ConfigureAwait(false);
            case 4:
                return await TryExchangeLegacyArthurVipQuestItemsAsync(
                        context,
                        session,
                        optionId,
                        [new LegacyArthurItemCost(LegacyArthurImpureVigorItemId, 5)],
                        LegacyArthurVipTokenItemId,
                        successText: null,
                        missingText: "I'm sorry, you need at least 5 ImpureVigor in order for me to help you.",
                        broadcastCompletion: true,
                        cancellationToken)
                    .ConfigureAwait(false);
            case 5:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 355, LegacyArthurSourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryExchangeLegacyArthurVipQuestItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        IReadOnlyList<LegacyArthurItemCost> costs,
        uint rewardItemId,
        string? successText,
        string missingText,
        bool broadcastCompletion,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc Arthur VipQuest exchange " +
                    $"{FormatSession(context)} source={LegacyArthurSourceMethod} option={optionId} " +
                    $"rewardItemId={rewardItemId} reason=inventory-full.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyArthurUnboundItemCosts(session.PlayerItems, costs))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyArthurSourceMethod,
                    [missingText],
                    [new LegacyNpcDialogOption("I see.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc Arthur VipQuest exchange " +
                    $"{FormatSession(context)} source={LegacyArthurSourceMethod} option={optionId} " +
                    $"rewardItemId={rewardItemId} reason=missing-materials.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyArthurVipQuestFenceAsync(
                    context,
                    optionId,
                    rewardItemId,
                    "session-store-missing; no item persistence or inventory mutation occurred")
                .ConfigureAwait(false);
            return true;
        }

        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await LogLegacyArthurVipQuestFenceAsync(
                    context,
                    optionId,
                    rewardItemId,
                    "player-items-root-missing; no item persistence or inventory mutation occurred")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadLegacyItemType(context.WorldData, rewardItemId, out var rewardItemType))
        {
            await LogLegacyArthurVipQuestFenceAsync(
                    context,
                    optionId,
                    rewardItemId,
                    "reward-itemtype-missing; no materials were consumed")
                .ConfigureAwait(false);
            return true;
        }

        if (rewardItemType.Id != checked((int)rewardItemId) ||
            rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await LogLegacyArthurVipQuestFenceAsync(
                    context,
                    optionId,
                    rewardItemId,
                    "reward-itemtype-invalid; no materials were consumed")
                .ConfigureAwait(false);
            return true;
        }

        PlayerItemSnapshotItem grantedItem;
        try
        {
            grantedItem = CreatePurchasedItems(
                    session.PlayerItems,
                    rewardItemType,
                    count: 1,
                    isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            await LogLegacyArthurVipQuestFenceAsync(
                    context,
                    optionId,
                    rewardItemId,
                    "item-uid-exhausted; no materials were consumed")
                .ConfigureAwait(false);
            return true;
        }

        var updatedItems = session.PlayerItems;
        var consumptionFrames = new List<byte[]>();
        foreach (var cost in costs)
        {
            for (var removed = 0U; removed < cost.Count; removed++)
            {
                // Inventory.Remove checks an unbound gate, then removes matching items without a bound filter.
                var material = updatedItems.InventoryItems
                    .Where(item => item.ItemId == cost.ItemId)
                    .OrderBy(item => item.Uid)
                    .FirstOrDefault();
                if (material is null)
                {
                    await LogLegacyArthurVipQuestFenceAsync(
                            context,
                            optionId,
                            rewardItemId,
                            $"material-count-drift:itemId={cost.ItemId}; no session mutation was committed")
                        .ConfigureAwait(false);
                    return true;
                }

                updatedItems = ConsumeSingleLegacyInventoryItem(
                    updatedItems,
                    material,
                    out var consumptionFrame);
                consumptionFrames.Add(consumptionFrame);
            }
        }

        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc Arthur VipQuest exchange persistence " +
                    $"{FormatSession(context)} source={LegacyArthurSourceMethod} option={optionId} " +
                    $"rewardItemId={rewardItemId} reason={ex.GetType().Name}; no session mutation was committed.")
                .ConfigureAwait(false);
            return true;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var frame in consumptionFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);

        if (broadcastCompletion)
        {
            var completionFrame = LegacyMsgMessagePacket.SystemTalk(
                    $"Congratulation! {updatedSession.Character.Name} has finished the Vip6 Quest.")
                .ToFrameBytes();
            foreach (var active in sessions.Snapshot())
            {
                await active.Connection.SendAsync(completionFrame, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await SendLegacyNpcDialogAsync(
                    updatedContext,
                    LegacyArthurSourceMethod,
                    [successText!],
                    [new LegacyNpcDialogOption("Thank you.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc Arthur VipQuest exchange " +
                $"{FormatSession(updatedContext)} source={LegacyArthurSourceMethod} option={optionId} " +
                $"rewardItemId={rewardItemId} bound=false broadcastCompletion={broadcastCompletion.ToString().ToLowerInvariant()}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool HasLegacyArthurUnboundItemCosts(
        PlayerItemSnapshot items,
        IReadOnlyList<LegacyArthurItemCost> costs)
    {
        foreach (var cost in costs)
        {
            var available = items.InventoryItems
                .Where(item => item.ItemId == cost.ItemId && item.Bound == 0)
                .Aggregate(0UL, (total, item) => checked(total + item.StackSize));
            if (available < cost.Count)
            {
                return false;
            }
        }

        return true;
    }

    private static ValueTask LogLegacyArthurVipQuestFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        uint rewardItemId,
        string reason)
    {
        return new ValueTask(
            context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc Arthur VipQuest exchange " +
                $"{FormatSession(context)} source={LegacyArthurSourceMethod} option={optionId} " +
                $"rewardItemId={rewardItemId} reason={reason}."));
    }

    private readonly record struct LegacyArthurItemCost(uint ItemId, uint Count);
}
