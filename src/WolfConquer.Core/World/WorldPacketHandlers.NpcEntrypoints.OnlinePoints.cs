using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyOnlinePointsSource = "NpcHandler.OnlinePoints";
    private const string LegacyOnlinePointsAttribute = "OnlineMinutes";
    private const ushort LegacyOnlinePointsAvatarId = 63;

    private static readonly LegacyNpcDialogOption[] LegacyOnlinePointsRootOptions =
    [
        new("10 donation points [2500 points].", 10),
        new("6 hours VIP 4 [120 points].", 21),
        new("6 hours VIP 6 [200 points].", 22),
        new("100,000 CPs [6 points].", 2),
        new("Soul Gem x10 [1000 points].", 4),
        new("I understand.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyOnlinePointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyOnlinePointsNpcId)
        {
            return false;
        }

        switch (optionId)
        {
            case 0:
            case 9:
                var onlinePoints = ReadAttributeUInt64(session.Character.Attributes, LegacyOnlinePointsAttribute);
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyOnlinePointsSource,
                        [
                            "You can exchange online points for rewards.",
                            "Want to continue? These are the exchangeable items.",
                            $"Your current online points: [{onlinePoints.ToString(CultureInfo.InvariantCulture)}]",
                        ],
                        LegacyOnlinePointsRootOptions,
                        LegacyOnlinePointsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                return await TryPurchaseLegacyOnlinePointsDonationPointsAsync(
                        context,
                        session,
                        optionId,
                        cost: 2_500,
                        donationPoints: 10,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 21:
                return await TryPurchaseLegacyOnlinePointsVipAsync(
                        context,
                        session,
                        optionId,
                        cost: 120,
                        vipLevel: 4,
                        requiresVipZero: false,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 22:
                return await TryPurchaseLegacyOnlinePointsVipAsync(
                        context,
                        session,
                        optionId,
                        cost: 200,
                        vipLevel: 6,
                        requiresVipZero: true,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 2:
                return await TryPurchaseLegacyOnlinePointsConquerPointsAsync(
                        context,
                        session,
                        optionId,
                        cost: 6,
                        conquerPoints: 100_000,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 4:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 4_200_010,
                        amount: 1,
                        cost: 1_000,
                        bound: false,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 14:
                return await TryPurchaseLegacyOnlinePointsRandomAccessoryAsync(
                        context,
                        session,
                        optionId,
                        cost: 100,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 1:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 3_001_044,
                        amount: 2,
                        cost: 10,
                        bound: false,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 3:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 730_005,
                        amount: 1,
                        cost: 1_000,
                        bound: false,
                        plus: 5,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 5:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 3_310_998,
                        amount: 1,
                        cost: 500,
                        bound: false,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 6:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 700_133,
                        amount: 1,
                        cost: 50,
                        bound: true,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 7:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 3_005_360,
                        amount: 1,
                        cost: 25,
                        bound: false,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 110:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 3_306_366,
                        amount: 5,
                        cost: 50,
                        bound: false,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 120:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 3_306_368,
                        amount: 5,
                        cost: 50,
                        bound: false,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);

            case 130:
                return await TryPurchaseLegacyOnlinePointsItemAsync(
                        context,
                        session,
                        optionId,
                        itemId: 3_306_367,
                        amount: 5,
                        cost: 50,
                        bound: false,
                        plus: 0,
                        cancellationToken)
                    .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TryPurchaseLegacyOnlinePointsDonationPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ulong cost,
        ulong donationPoints,
        CancellationToken cancellationToken)
    {
        var onlinePoints = ReadAttributeUInt64(session.Character.Attributes, LegacyOnlinePointsAttribute);
        if (onlinePoints < cost)
        {
            await SendLegacyOnlinePointsInsufficientPointsAsync(context, optionId, onlinePoints, cost, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedOnlinePoints = onlinePoints - cost;
        var updatedDonationPoints = checked(ReadAttributeUInt64(session.Character.Attributes, "DonatePoints") + donationPoints);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(session.Character, LegacyOnlinePointsAttribute, updatedOnlinePoints),
            "DonatePoints",
            updatedDonationPoints);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await PersistLegacyOnlinePointsCharacterAsync(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyOnlinePointsAttribute] = updatedOnlinePoints.ToString(CultureInfo.InvariantCulture),
                    ["DonatePoints"] = updatedDonationPoints.ToString(CultureInfo.InvariantCulture),
                },
                "donation-points purchase")
            .ConfigureAwait(false);

        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacyOnlinePointsSource,
                [$"Congratulations! You used {cost.ToString(CultureInfo.InvariantCulture)} points to receive {donationPoints.ToString(CultureInfo.InvariantCulture)} donation points."],
                [new LegacyNpcDialogOption("Thanks.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc OnlinePoints donation-points purchase " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                $"option={optionId} cost={cost} onlinePoints={updatedOnlinePoints} donatePoints={updatedDonationPoints}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryPurchaseLegacyOnlinePointsConquerPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ulong cost,
        long conquerPoints,
        CancellationToken cancellationToken)
    {
        var onlinePoints = ReadAttributeUInt64(session.Character.Attributes, LegacyOnlinePointsAttribute);
        if (onlinePoints < cost)
        {
            await SendLegacyOnlinePointsInsufficientPointsAsync(context, optionId, onlinePoints, cost, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedOnlinePoints = onlinePoints - cost;
        var updatedConquerPoints = checked(session.Character.ConquerPoints + conquerPoints);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                session.Character with { ConquerPoints = updatedConquerPoints },
                LegacyOnlinePointsAttribute,
                updatedOnlinePoints),
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await PersistLegacyOnlinePointsCharacterAsync(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyOnlinePointsAttribute] = updatedOnlinePoints.ToString(CultureInfo.InvariantCulture),
                    ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                "conquer-points purchase")
            .ConfigureAwait(false);

        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacyOnlinePointsSource,
                ["Congratulations! You received 100,000 CPs!"],
                [new LegacyNpcDialogOption("Thanks.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc OnlinePoints conquer-points purchase " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                $"option={optionId} cost={cost} onlinePoints={updatedOnlinePoints} conquerPoints={updatedConquerPoints}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryPurchaseLegacyOnlinePointsVipAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ulong cost,
        ulong vipLevel,
        bool requiresVipZero,
        CancellationToken cancellationToken)
    {
        var onlinePoints = ReadAttributeUInt64(session.Character.Attributes, LegacyOnlinePointsAttribute);
        if (onlinePoints < cost)
        {
            await SendLegacyOnlinePointsInsufficientPointsAsync(context, optionId, onlinePoints, cost, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var currentVipLevel = ReadAttributeUInt64(session.Character.Attributes, "VipLevel", "VIPLevel");
        var alreadyHasVip = requiresVipZero ? currentVipLevel != 0 : currentVipLevel >= vipLevel;
        if (alreadyHasVip)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyOnlinePointsSource,
                    [$"You already have VIP {currentVipLevel.ToString(CultureInfo.InvariantCulture)}!"],
                    [new LegacyNpcDialogOption("I know.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc OnlinePoints VIP purchase " +
                    $"{FormatSession(context)} source={LegacyOnlinePointsSource} option={optionId} " +
                    $"cost={cost} onlinePoints={onlinePoints} currentVipLevel={currentVipLevel}.")
                .ConfigureAwait(false);
            return true;
        }

        var updatedOnlinePoints = onlinePoints - cost;
        var vipExpiresAt = DateTime.Now.AddHours(6).ToBinary();
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(session.Character, LegacyOnlinePointsAttribute, updatedOnlinePoints),
                "VipLevel",
                vipLevel),
            "VipTime",
            vipExpiresAt.ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await PersistLegacyOnlinePointsCharacterAsync(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyOnlinePointsAttribute] = updatedOnlinePoints.ToString(CultureInfo.InvariantCulture),
                    ["VipLevel"] = vipLevel.ToString(CultureInfo.InvariantCulture),
                    ["VipTime"] = vipExpiresAt.ToString(CultureInfo.InvariantCulture),
                },
                "VIP purchase")
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(updatedCharacter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyVipStatusPacket.FromSnapshot(updatedCharacter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacyOnlinePointsSource,
                [$"Congratulations! You received 6 hours of VIP {vipLevel.ToString(CultureInfo.InvariantCulture)} service!"],
                [new LegacyNpcDialogOption("Thanks.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc OnlinePoints VIP purchase " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                $"option={optionId} cost={cost} onlinePoints={updatedOnlinePoints} vipLevel={vipLevel}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryPurchaseLegacyOnlinePointsItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint itemId,
        int amount,
        ulong cost,
        bool bound,
        byte plus,
        CancellationToken cancellationToken,
        ItemType? knownItemType = null,
        string? successMessage = null)
    {
        var onlinePoints = ReadAttributeUInt64(session.Character.Attributes, LegacyOnlinePointsAttribute);
        if (onlinePoints < cost)
        {
            await SendLegacyOnlinePointsInsufficientPointsAsync(context, optionId, onlinePoints, cost, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, amount))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyOnlinePointsSource,
                    ["Your inventory does not have enough space!"],
                    [new LegacyNpcDialogOption("I know.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc OnlinePoints item purchase " +
                    $"{FormatSession(context)} source={LegacyOnlinePointsSource} option={optionId} " +
                    $"itemId={itemId} amount={amount} cost={cost}; inventory is full.")
                .ConfigureAwait(false);
            return true;
        }

        var itemType = knownItemType;
        if (itemType is null && !TryReadItemType(context.WorldData, itemId, out itemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc OnlinePoints item purchase missing item type " +
                    $"{FormatSession(context)} source={LegacyOnlinePointsSource} option={optionId} " +
                    $"itemId={itemId}; grant uses zero durability.")
                .ConfigureAwait(false);
        }

        var grantedItems = itemType is null
            ? CreateLegacyOnlinePointsFallbackItems(session.PlayerItems, itemId, amount, bound, plus)
            : CreatePurchasedItems(session.PlayerItems, itemType, amount, bound, plus);
        var updatedOnlinePoints = onlinePoints - cost;
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat(grantedItems).ToArray(),
        };
        var updatedCharacter = WithCharacterAttribute(session.Character, LegacyOnlinePointsAttribute, updatedOnlinePoints);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        await PersistLegacyOnlinePointsCharacterAsync(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyOnlinePointsAttribute] = updatedOnlinePoints.ToString(CultureInfo.InvariantCulture),
                },
                "item purchase")
            .ConfigureAwait(false);
        await PersistLegacyOnlinePointsItemsAsync(context, updatedSession, itemId).ConfigureAwait(false);

        foreach (var item in grantedItems)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacyOnlinePointsSource,
                [successMessage ?? $"Congratulations! You received {amount.ToString(CultureInfo.InvariantCulture)} item(s): {itemId.ToString(CultureInfo.InvariantCulture)}."],
                [new LegacyNpcDialogOption("Thanks.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc OnlinePoints item purchase " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                $"option={optionId} itemId={itemId} amount={amount} cost={cost} onlinePoints={updatedOnlinePoints}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryPurchaseLegacyOnlinePointsRandomAccessoryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        ulong cost,
        CancellationToken cancellationToken)
    {
        var onlinePoints = ReadAttributeUInt64(session.Character.Attributes, LegacyOnlinePointsAttribute);
        if (onlinePoints < cost)
        {
            await SendLegacyOnlinePointsInsufficientPointsAsync(context, optionId, onlinePoints, cost, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var accessories = context.WorldData?.Items
            .Where(item => item.IsLegacyAccessory)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .ToArray() ?? [];
        if (accessories.Length == 0)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc OnlinePoints random accessory purchase missing ItemType.Accessorys " +
                    $"{FormatSession(context)} source={LegacyOnlinePointsSource} option={optionId} cost={cost}.")
                .ConfigureAwait(false);
            return true;
        }

        var selected = accessories[Random.Shared.Next(0, accessories.Length)];
        return await TryPurchaseLegacyOnlinePointsItemAsync(
                context,
                session,
                optionId,
                checked((uint)selected.Id),
                amount: 1,
                cost,
                bound: false,
                plus: 0,
                cancellationToken,
                knownItemType: selected,
                successMessage: $"Congratulations! You received a {selected.Name}!")
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<PlayerItemSnapshotItem> CreateLegacyOnlinePointsFallbackItems(
        PlayerItemSnapshot items,
        uint itemId,
        int amount,
        bool bound,
        byte plus)
    {
        var nextUid = GetNextSessionItemUid(items);
        var granted = new PlayerItemSnapshotItem[amount];
        for (var index = 0; index < amount; index++)
        {
            granted[index] = new PlayerItemSnapshotItem
            {
                Uid = checked(nextUid + (uint)index),
                ItemId = itemId,
                Position = 0,
                StackSize = 1,
                Bound = bound ? (byte)1 : (byte)0,
                Plus = plus,
            };
        }

        return granted;
    }

    private static async ValueTask SendLegacyOnlinePointsInsufficientPointsAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        ulong onlinePoints,
        ulong cost,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyOnlinePointsSource,
                ["You do not have enough online points!"],
                [new LegacyNpcDialogOption("I know.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc OnlinePoints purchase " +
                $"{FormatSession(context)} source={LegacyOnlinePointsSource} option={optionId} " +
                $"cost={cost} onlinePoints={onlinePoints}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyOnlinePointsCharacterAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        IReadOnlyDictionary<string, string> values,
        string action)
    {
        if (TryPersistLegacyCharacterAttributes(
                updatedSession.UsersRoot,
                updatedSession.AccountId,
                updatedSession.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc OnlinePoints character " +
                    $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                    $"action=\"{action}\" path={Path.GetFileName(path)} keys={string.Join(",", values.Keys)}.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc OnlinePoints character persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                    $"action=\"{action}\" reason={reason}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask PersistLegacyOnlinePointsItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        uint itemId)
    {
        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    updatedSession.Character.Uid,
                    updatedSession.PlayerItems);
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc OnlinePoints items " +
                        $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                        $"path={updatedSession.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin itemId={itemId}.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc OnlinePoints item persistence " +
                        $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                        $"itemId={itemId} reason={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc OnlinePoints missing PlayerItemsRoot " +
                    $"{FormatSession(context with { Session = updatedSession })} source={LegacyOnlinePointsSource} " +
                    $"itemId={itemId}; granted item is session-only.")
                .ConfigureAwait(false);
        }
    }
}
