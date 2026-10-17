using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesDazzlingDiamondBoxNpcId = 4_294_967_279;
    private const uint LegacyItemPackServicesDazzlingDiamondBoxItemId = 3_008_732;
    private const ushort LegacyItemPackServicesDazzlingDiamondRewardCount = 30;
    private const int LegacyItemPackServicesDazzlingDiamondStackSizeFieldIndex = 46;

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleDazzlingDiamondBoxAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.DazzlingDiamondBox";
        if (!LegacyItemPackServicesHasUnboundDazzlingDiamondBox(session))
        {
            await LegacyItemPackServicesSendMessageBoxAsync(
                    context,
                    source,
                    "Sorry, you don`t have a DazzlingDiamondBox.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["Hello. You can chose a 30 pieces Diamond: Sun~Diamond, Moon~Diamond, Star~Diamond or Cloud~Diamond."],
                    [
                        new("Sun~Diamond", 1),
                        new("Moon~Diamond", 2),
                        new("Star~Diamond", 3),
                        new("Cloud~Diamond", 4),
                        new("Nevermind", LegacyNpcClosedOption),
                    ],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is >= 1 and <= 4)
        {
            if (!HasLegacyInventorySpace(session.PlayerItems, 30))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Please make 30 spaces in your inventory."],
                        [new LegacyNpcDialogOption("Let~me~check.", LegacyNpcClosedOption)],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await LegacyItemPackServicesExchangeDazzlingDiamondBoxAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    source,
                    optionId switch
                    {
                        1 => "30056-30061",
                        2 => "30068-30073",
                        3 => "30080-30085",
                        _ => "30092-30097",
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static bool LegacyItemPackServicesHasUnboundDazzlingDiamondBox(WorldSession session)
    {
        return session.PlayerItems.InventoryItems.Any(item =>
            item.ItemId == LegacyItemPackServicesDazzlingDiamondBoxItemId &&
            item.Bound == 0 &&
            item.StackSize > 0);
    }

    private static async ValueTask LegacyItemPackServicesExchangeDazzlingDiamondBoxAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        var rewardItemId = optionId switch
        {
            1 => 721_533U,
            2 => 721_534U,
            3 => 721_535U,
            _ => 721_536U,
        };
        var diamondName = optionId switch
        {
            1 => "Sun",
            2 => "Moon",
            3 => "Star",
            _ => "Cloud",
        };

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    sourceLines,
                    rewardItemId,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    sourceLines,
                    rewardItemId,
                    "player-items-root-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, rewardItemId, out var rewardItemType))
        {
            await LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    sourceLines,
                    rewardItemId,
                    "reward-itemtype-missing")
                .ConfigureAwait(false);
            return;
        }

        if (rewardItemType.Id != checked((int)rewardItemId) ||
            rewardItemType.LegacyDurability > ushort.MaxValue ||
            rewardItemType.Fields.Count <= LegacyItemPackServicesDazzlingDiamondStackSizeFieldIndex ||
            !ushort.TryParse(
                rewardItemType.Fields[LegacyItemPackServicesDazzlingDiamondStackSizeFieldIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stackLimit))
        {
            await LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    sourceLines,
                    rewardItemId,
                    "reward-itemtype-invalid")
                .ConfigureAwait(false);
            return;
        }

        var box = session.PlayerItems.InventoryItems.FirstOrDefault(item =>
            item.ItemId == LegacyItemPackServicesDazzlingDiamondBoxItemId &&
            item.StackSize > 0);
        if (!LegacyItemPackServicesHasUnboundDazzlingDiamondBox(session) || box is null)
        {
            await LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    sourceLines,
                    rewardItemId,
                    "box-count-drift")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshot updatedItems;
        byte[] consumptionFrame;
        IReadOnlyList<(PlayerItemSnapshotItem Item, ushort Mode)> rewardMutations;
        try
        {
            var consumedItems = ConsumeSingleLegacyInventoryItem(
                session.PlayerItems,
                box,
                out consumptionFrame);
            updatedItems = LegacyItemPackServicesAddDazzlingDiamonds(
                session.PlayerItems,
                consumedItems,
                rewardItemType,
                rewardItemId,
                stackLimit,
                out rewardMutations);
        }
        catch (OverflowException)
        {
            await LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    sourceLines,
                    rewardItemId,
                    "item-uid-exhausted")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { PlayerItems = updatedItems };
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
                    context,
                    npcId,
                    optionId,
                    source,
                    sourceLines,
                    rewardItemId,
                    "persistence-" + ex.GetType().Name)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        foreach (var mutation in rewardMutations)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(mutation.Item, mode: mutation.Mode).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (mutation.Mode == LegacyGameItemPacket.AddItemMode)
            {
                await SendLegacyItemFollowupsAsync(updatedContext, mutation.Item, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await LegacyItemPackServicesSendMessageBoxAsync(
                updatedContext,
                source,
                $"You receive 30 pieces of the {diamondName}~Diamond.",
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ItemPackServices Dazzling Diamond Box exchange " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId.ToString(CultureInfo.InvariantCulture)} " +
                $"optionId={optionId.ToString(CultureInfo.InvariantCulture)} " +
                $"boxItemId={LegacyItemPackServicesDazzlingDiamondBoxItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardItemId={rewardItemId.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardCount={LegacyItemPackServicesDazzlingDiamondRewardCount.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardMutations={rewardMutations.Count.ToString(CultureInfo.InvariantCulture)} bound=false.")
            .ConfigureAwait(false);
    }

    private static PlayerItemSnapshot LegacyItemPackServicesAddDazzlingDiamonds(
        PlayerItemSnapshot originalItems,
        PlayerItemSnapshot consumedItems,
        ItemType rewardItemType,
        uint rewardItemId,
        ushort stackLimit,
        out IReadOnlyList<(PlayerItemSnapshotItem Item, ushort Mode)> mutations)
    {
        if (stackLimit > 0)
        {
            var compatibleStack = consumedItems.InventoryItems.FirstOrDefault(item =>
                item.ItemId == rewardItemId &&
                item.Bound == 0 &&
                (uint)item.StackSize + LegacyItemPackServicesDazzlingDiamondRewardCount <= stackLimit);
            if (compatibleStack is not null)
            {
                var updatedStack = compatibleStack.WithStackSize(
                    checked((ushort)(compatibleStack.StackSize + LegacyItemPackServicesDazzlingDiamondRewardCount)));
                mutations = [(updatedStack, LegacyGameItemPacket.UpdateItemMode)];
                return ReplaceInventoryItem(consumedItems, compatibleStack, updatedStack);
            }
        }

        var stackSizes = new List<ushort>();
        if (stackLimit == 0)
        {
            for (var index = 0; index < LegacyItemPackServicesDazzlingDiamondRewardCount; index++)
            {
                stackSizes.Add(1);
            }
        }
        else
        {
            var remaining = LegacyItemPackServicesDazzlingDiamondRewardCount;
            while (remaining >= stackLimit)
            {
                stackSizes.Add(stackLimit);
                remaining = checked((ushort)(remaining - stackLimit));
            }

            if (remaining > 0)
            {
                stackSizes.Add(remaining);
            }
        }

        var nextUid = GetNextSessionItemUid(originalItems);
        var durability = checked((ushort)rewardItemType.LegacyDurability);
        var grantedItems = new PlayerItemSnapshotItem[stackSizes.Count];
        var createdMutations = new (PlayerItemSnapshotItem Item, ushort Mode)[stackSizes.Count];
        for (var index = 0; index < grantedItems.Length; index++)
        {
            var grantedItem = new PlayerItemSnapshotItem
            {
                Uid = checked(nextUid + (uint)index),
                ItemId = rewardItemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                StackSize = stackSizes[index],
                Bound = 0,
                Color = checked((uint)Random.Shared.Next(3, 9)),
            };
            grantedItems[index] = grantedItem;
            createdMutations[index] = (grantedItem, LegacyGameItemPacket.AddItemMode);
        }

        mutations = createdMutations;
        return consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems
                .Concat(grantedItems)
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
    }

    private static async ValueTask LogLegacyItemPackServicesDazzlingDiamondFenceAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        string source,
        string sourceLines,
        uint rewardItemId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ItemPackServices Dazzling Diamond Box exchange " +
                $"{FormatSession(context)} source={source} sourceLines={sourceLines} " +
                $"npcId={npcId.ToString(CultureInfo.InvariantCulture)} " +
                $"optionId={optionId.ToString(CultureInfo.InvariantCulture)} " +
                $"rewardItemId={rewardItemId.ToString(CultureInfo.InvariantCulture)} reason={reason}; " +
                "no item persistence, session mutation, or exchange packets were committed.")
            .ConfigureAwait(false);
    }
}
