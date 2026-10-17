using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyJailGuildServicesMillionaireLeeNpcId = 5_004;
    private const uint LegacyJailGuildServicesUnknowManNpcId = 3_825;
    private const uint LegacyJailGuildServicesLotteryCollectorWongNpcId = 3_952;
    private const uint LegacyJailGuildServicesLotteryLadyLuckNpcId = 924;
    private const uint LegacyJailGuildServicesMarketLadyLuckNpcId = 923;
    private const uint LegacyJailGuildServicesDisguiseAmuletItemId = 723_724;
    private const uint LegacyJailGuildServicesLifeFruitBasketItemId = 723_725;
    private const uint LegacyJailGuildServicesPenitenceAmuletItemId = 723_727;
    private const uint LegacyJailGuildServicesLotteryTicketItemId = 710_212;
    private const uint LegacyJailGuildServicesSmallLotteryTicketItemId = 711_504;
    private const long LegacyJailGuildServicesLotteryCollectorConquerPointReward = 15;

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesMillionaireLeeSource =
        new("NpcHandler.MillionaireLee", "39725-40127");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesUnknowManSource =
        new("NpcHandler.UnknowMan", "40129-40210");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesLotteryCollectorWongSource =
        new("NpcHandler.LotteryCollectorWong", "40212-40269");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesLotteryLadyLuckSource =
        new("NpcHandler.LotteryLadyLuck", "40271-40293");

    private static readonly LegacyJailGuildServicesSourceInfo LegacyJailGuildServicesMarketLadyLuckSource =
        new("NpcHandler.MarketLadyLuck", "40295-40338");

    private static readonly IReadOnlyDictionary<byte, LegacyJailGuildServicesPackConfirmation> LegacyJailGuildServicesMillionairePackConfirmations =
        new Dictionary<byte, LegacyJailGuildServicesPackConfirmation>
        {
            [1] = new("5 +1 Stones can be packed into a +1 Stone Pack. Would you like me to pack them for you?", 2),
            [3] = new("10 +2 Stones can be packed into a +2 Stone Pack. Would you like me to pack them for you?", 4),
            [5] = new("10 +3 Stones can be packed into a +3 Stone Pack. Would you like me to pack them for you?", 6),
            [8] = new("5 Normal Glory Gems can be packed into a Glory Gem Pack. Would you like me to pack them for you?", 20),
            [9] = new("5 Normal Thunder Gems can be packed into a Thunder Gem Pack. Would you like me to pack them for you?", 21),
            [10] = new("5 Normal Kylin Gems can be packed into a Kylin Gem Pack. Would you like me to pack them for you?", 22),
            [11] = new("5 Normal Rainbow Gems can be packed into a Rainbow Gem Pack. Would you like me to pack them for you?", 23),
            [12] = new("5 Normal Fury Gems can be packed into a Fury Gem Pack. Would you like me to pack them for you?", 24),
            [14] = new("5 Normal Dragon Gems can be packed into a Dragon Gem Pack. Would you like me to pack them for you?", 25),
            [15] = new("5 Normal Phoenix Gems can be packed into a Phoenix Gem Pack. Would you like me to pack them for you?", 26),
            [16] = new("5 Normal Violet Gems can be packed into a Violet Gem Pack. Would you like me to pack them for you?", 27),
            [17] = new("5 Normal Moon Gems can be packed into a Moon Gem Pack. Would you like me to pack them for you?", 28),
            [18] = new("5 Normal Tortoise Gems can be packed into a Tortoise Gem Pack. Would you like me to pack them for you?", 29),
            [32] = new("Pay 100,000 silver and give me 5 EXP Balls. I can pack them into an EXP Ball Gift Box for you.", 33),
            [34] = new("10 Meteors can be packed into a Meteor Pack. Would you like me to pack them for you?", 35),
            [36] = new("10 DragonBalls can be packed into a DragonBall Pack. Would you like me to pack them for you?", 37),
        };

    private static readonly HashSet<byte> LegacyJailGuildServicesMillionaireMutationOptions =
    [
        2,
        4,
        6,
        20,
        21,
        22,
        23,
        24,
        25,
        26,
        27,
        28,
        29,
        33,
        35,
        37,
    ];

    private readonly record struct LegacyJailGuildServicesPackConfirmation(string Text, byte ConfirmOptionId);

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesMillionaireLeeAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMillionaireLeeSource.SourceMethod,
                        ["Hunting and mining for treasure is exciting, but those items can pile up in your inventory."],
                        [
                            new("Pack +1 Stones.", 1),
                            new("Pack +2 Stones.", 3),
                            new("Pack +3 Stones.", 5),
                            new("Pack Normal Gems.", 7),
                            new("Pack EXP Balls for me.", 32),
                            new("Pack Meteors.", 34),
                            new("Pack DragonBalls.", 36),
                            new("Just passing by.", LegacyNpcClosedOption),
                        ],
                        avatarId: 7,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 7:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMillionaireLeeSource.SourceMethod,
                        ["Which kind of Normal Gems do you want to pack?"],
                        [
                            new("Glory Gems.", 8),
                            new("Thunder Gems.", 9),
                            new("Kylin Gems.", 10),
                            new("Rainbow Gems.", 11),
                            new("Fury Gems.", 12),
                            new("Next page.", 13),
                        ],
                        avatarId: 7,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 13:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMillionaireLeeSource.SourceMethod,
                        ["Which kind of Normal Gems do you want to pack?"],
                        [
                            new("Dragon Gems.", 14),
                            new("Phoenix Gems.", 15),
                            new("Violet Gems.", 16),
                            new("Moon Gems.", 17),
                            new("Tortoise Gems.", 18),
                            new("Back page.", 7),
                            new("Just passing by.", LegacyNpcClosedOption),
                        ],
                        avatarId: 7,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 19:
            case 30:
            case 31:
                return true;
        }

        if (LegacyJailGuildServicesMillionairePackConfirmations.TryGetValue(optionId, out var confirmation))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesMillionaireLeeSource.SourceMethod,
                    [confirmation.Text],
                    [
                        new("Yes, please.", confirmation.ConfirmOptionId),
                        new("Bye.", LegacyNpcClosedOption),
                    ],
                    avatarId: 7,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyJailGuildServicesMillionaireMutationOptions.Contains(optionId))
        {
            await SendLegacyJailGuildServicesUnavailableAsync(
                    context,
                    LegacyJailGuildServicesMillionaireLeeSource,
                    LegacyJailGuildServicesMillionaireLeeNpcId,
                    optionId,
                    "inventory pack removal and grant are not modeled",
                    avatarId: 7,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesUnknowManAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesUnknowManSource.SourceMethod,
                        [
                            "The DragonBall is a precious item. You can use it to refine equipment.",
                            "If you use it properly, you can gain a large amount of EXP from it.",
                        ],
                        [
                            new("Please help me.", 1),
                            new("Definitely.", LegacyNpcClosedOption),
                        ],
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesUnknowManSource.SourceMethod,
                        ["According to your ability, I can help you gain EXP from 1 DragonBall each day."],
                        [
                            new("Please help me.", 2),
                            new("I see.", LegacyNpcClosedOption),
                        ],
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                if (session.Character.Level >= 140)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesUnknowManSource.SourceMethod,
                            ["Sorry, your level is already too high for this service."],
                            LegacyJailGuildServicesISeeOptions,
                            avatarId: 67,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyJailGuildServicesDragonBallItemId, 1))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesUnknowManSource.SourceMethod,
                            ["Sorry, you do not have a DragonBall."],
                            LegacyJailGuildServicesISeeOptions,
                            avatarId: 67,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyJailGuildServicesUnavailableAsync(
                        context,
                        LegacyJailGuildServicesUnknowManSource,
                        LegacyJailGuildServicesUnknowManNpcId,
                        optionId,
                        "DragonBall removal, daily DbTry state, and EXP grant are not modeled",
                        avatarId: 67,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesLotteryCollectorWongAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod,
                    [
                        "I collect all kinds of treasure.",
                        "If you have Disguise Amulets, Life Fruit Baskets, or Penitence Amulets, I will pay 15 CPs for each one.",
                    ],
                    [
                        new("Here are my Disguise Amulets.", 1),
                        new("Here are my Life Fruit Baskets.", 2),
                        new("Here are my Penitence Amulets.", 3),
                        new("I do not believe it.", LegacyNpcClosedOption),
                    ],
                    avatarId: 10,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is not (1 or 2 or 3))
        {
            return false;
        }

        var sourceItemId = optionId switch
        {
            1 => LegacyJailGuildServicesDisguiseAmuletItemId,
            2 => LegacyJailGuildServicesLifeFruitBasketItemId,
            3 => LegacyJailGuildServicesPenitenceAmuletItemId,
            _ => 0U,
        };
        if (context.Session is { } gateSession &&
            !HasLegacyJailGuildServicesUnboundItem(gateSession.PlayerItems, sourceItemId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod,
                    ["You~do~not~have~what~I~want.~Whenever~you~have~one~of~DisguiseAmulets,~LifeFruitBaskets~or~PenitenceAmulets,~just~come~here."],
                    [new LegacyNpcDialogOption("See~you~again.", LegacyNpcClosedOption)],
                    avatarId: 10,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (context.Session is not { } session ||
            context.Sessions is null ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) ||
            string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await SendLegacyJailGuildServicesUnavailableAsync(
                    context,
                    LegacyJailGuildServicesLotteryCollectorWongSource,
                    LegacyJailGuildServicesLotteryCollectorWongNpcId,
                    optionId,
                    "lottery collector exchange requires session, player-item, and character-wallet persistence",
                    avatarId: 10,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await ExchangeLegacyJailGuildServicesLotteryCollectorItemAsync(
                context,
                session,
                optionId,
                sourceItemId,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesLotteryLadyLuckAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesLotteryLadyLuckSource.SourceMethod,
                        ["Look. Here is the LuckyBox, a magical box that may make your dream come true."],
                        [
                            new("Get me out of here.", 1),
                            new("Tell me more about it.", 2),
                            new("I do not believe it.", LegacyNpcClosedOption),
                        ],
                        avatarId: 159,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyJailGuildServicesLotteryLadyLuckNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, LegacyJailGuildServicesLotteryLadyLuckSource.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesLotteryLadyLuckSource.SourceMethod,
                        [
                            "You have a chance to get whatever you desire from a LuckyBox.",
                            "It may be a DragonBall, a +8 weapon, or two-socket boots. It all depends on your luck.",
                        ],
                        [new LegacyNpcDialogOption("Thanks a lot.", LegacyNpcClosedOption)],
                        avatarId: 123,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyJailGuildServicesMarketLadyLuckAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod,
                        [
                            "Welcome to the Lottery Center. There are many Lucky Boxes here, each containing incredible treasures.",
                            "Pay 3 Small Lottery Tickets to draw from a Lucky Box. You may enter up to 10 times a day, with extra chances for VIP players.",
                        ],
                        [
                            new("Let us try my luck.", 1),
                            new("Exchange Small Lottery Ticket.", 2),
                            new("May I know the rules first?", 3),
                            new("Nice to see you.", LegacyNpcClosedOption),
                        ],
                        avatarId: 123,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.Level < 70)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod,
                            ["Sorry, you need level 70."],
                            [new LegacyNpcDialogOption("Oops.", LegacyNpcClosedOption)],
                            avatarId: 123,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyJailGuildServicesMarketLadyLuckNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(700, 39, 45, LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await ExchangeLegacyJailGuildServicesMarketLotteryTicketAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod,
                        [
                            "Pay 3 Small Lottery Tickets to draw from the Lucky Box in the Lottery Center.",
                            "If you are not satisfied with the item, you can pay 1 more Small Lottery Ticket to change it up to two times.",
                            "Each player can draw up to 10 times per day.",
                        ],
                        [new LegacyNpcDialogOption("Okay, I see.", LegacyNpcClosedOption)],
                        avatarId: 123,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask ExchangeLegacyJailGuildServicesLotteryCollectorItemAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint sourceItemId,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints < 0 ||
            session.Character.ConquerPoints > long.MaxValue - LegacyJailGuildServicesLotteryCollectorConquerPointReward)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices LotteryCollectorWong exchange " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesLotteryCollectorWongSource.SourceLines} optionId={optionId} " +
                    "reason=conquer-points-out-of-range; no item or wallet mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Uid is < uint.MinValue or > uint.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices LotteryCollectorWong exchange " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesLotteryCollectorWongSource.SourceLines} optionId={optionId} " +
                    "reason=character-uid-out-of-range; no item or wallet mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        var characterUid = (uint)session.Character.Uid;

        var removalTarget = session.PlayerItems.InventoryItems.First(item => item.ItemId == sourceItemId);
        var updatedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            removalTarget,
            out var removalFrame);
        var updatedConquerPoints = checked(
            session.Character.ConquerPoints + LegacyJailGuildServicesLotteryCollectorConquerPointReward);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        var walletFrame = new LegacyMsgUpdatePacket(
                characterUid,
                [new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.ConquerPoints,
                    checked((ulong)updatedConquerPoints))])
            .ToFrameBytes();
        var characterSnapshotPath = Path.Combine(
            session.UsersRoot!,
            session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        byte[] originalCharacterBytes;
        try
        {
            originalCharacterBytes = File.ReadAllBytes(characterSnapshotPath);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices LotteryCollectorWong exchange persistence " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesLotteryCollectorWongSource.SourceLines} optionId={optionId} " +
                    $"itemId={sourceItemId} reason=character-preflight-{ex.GetType().Name}; " +
                    "no item, wallet, session, or packet mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        var itemLoader = new PlayerItemSnapshotLoader(context.PlayerItemsRoot!);
        try
        {
            itemLoader.SaveByCharacterUid(updatedCharacter.Uid, updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices LotteryCollectorWong exchange persistence " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesLotteryCollectorWongSource.SourceLines} optionId={optionId} " +
                    $"itemId={sourceItemId} reason={ex.GetType().Name}; no session, packet, item, or wallet mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        var walletValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
        };
        string walletPath;
        string walletReason;
        bool walletPersisted;
        try
        {
            walletPersisted = TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                walletValues,
                out walletPath,
                out walletReason);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            walletPersisted = false;
            walletPath = characterSnapshotPath;
            walletReason = ex.GetType().Name;
        }

        if (!walletPersisted)
        {
            var itemRollbackReason = "complete";
            try
            {
                itemLoader.SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
            {
                itemRollbackReason = "failed:" + ex.GetType().Name;
            }

            var characterRollbackReason = "complete";
            try
            {
                File.WriteAllBytes(characterSnapshotPath, originalCharacterBytes);
            }
            catch (Exception ex) when (
                ex is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
            {
                characterRollbackReason = "failed:" + ex.GetType().Name;
            }

            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices LotteryCollectorWong exchange persistence " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesLotteryCollectorWongSource.SourceLines} optionId={optionId} " +
                    $"itemId={sourceItemId} walletPath={walletPath} reason={walletReason} " +
                    $"itemRollback={itemRollbackReason} characterRollback={characterRollbackReason}; " +
                    "no session or mutation packet was committed.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions!.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(removalFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(walletFrame, cancellationToken).ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod,
                ["You receive 15 ConquerPoints for you item."],
                [new LegacyNpcDialogOption("Ok~thank~you.", LegacyNpcClosedOption)],
                avatarId: 10,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc JailGuildServices LotteryCollectorWong exchange " +
                $"{FormatSession(updatedContext)} source={LegacyJailGuildServicesLotteryCollectorWongSource.SourceMethod} " +
                $"sourceLines={LegacyJailGuildServicesLotteryCollectorWongSource.SourceLines} optionId={optionId} " +
                $"itemId={sourceItemId} removedUid={removalTarget.Uid} removedBound={removalTarget.Bound} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                "sourceOrder=remove-item+conquer-points+dialog.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ExchangeLegacyJailGuildServicesMarketLotteryTicketAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is null || string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await SendLegacyJailGuildServicesUnavailableAsync(
                    context,
                    LegacyJailGuildServicesMarketLadyLuckSource,
                    LegacyJailGuildServicesMarketLadyLuckNpcId,
                    optionId,
                    "lottery ticket exchange requires session and player-item persistence",
                    avatarId: 123,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyJailGuildServicesUnboundItem(
                session.PlayerItems,
                LegacyJailGuildServicesLotteryTicketItemId))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod,
                    ["You don`t have any Lottery Tickets to exchange for the Small Lottery Tickets."],
                    [new LegacyNpcDialogOption("Oops.", LegacyNpcClosedOption)],
                    avatarId: 123,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(
                context.WorldData,
                LegacyJailGuildServicesSmallLotteryTicketItemId,
                out var rewardItemType) ||
            rewardItemType.Id != LegacyJailGuildServicesSmallLotteryTicketItemId ||
            rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices MarketLadyLuck exchange " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesMarketLadyLuckSource.SourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyJailGuildServicesSmallLotteryTicketItemId} reason=itemtype-missing-or-invalid; " +
                    "no ticket was consumed and no mutation packet was sent.")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem rewardItem;
        try
        {
            rewardItem = CreatePurchasedItems(
                session.PlayerItems,
                rewardItemType,
                count: 1,
                isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
        }
        catch (OverflowException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices MarketLadyLuck exchange " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesMarketLadyLuckSource.SourceLines} optionId={optionId} " +
                    "reason=item-uid-exhausted; no ticket was consumed and no mutation packet was sent.")
                .ConfigureAwait(false);
            return;
        }

        var removalTarget = session.PlayerItems.InventoryItems.First(
            item => item.ItemId == LegacyJailGuildServicesLotteryTicketItemId);
        var consumedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            removalTarget,
            out var removalFrame);
        if (!HasLegacyInventorySpace(consumedItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices MarketLadyLuck exchange " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesMarketLadyLuckSource.SourceLines} optionId={optionId} " +
                    "reason=inventory-full-after-planned-ticket-removal; no ticket was consumed and no reward was granted.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        var rewardFrame = LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes();

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
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc JailGuildServices MarketLadyLuck exchange persistence " +
                    $"{FormatSession(context)} source={LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod} " +
                    $"sourceLines={LegacyJailGuildServicesMarketLadyLuckSource.SourceLines} optionId={optionId} " +
                    $"reason={ex.GetType().Name}; no session or mutation packet was committed.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions.Set(context.Connection, updatedSession);
        await context.Connection.SendAsync(removalFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(rewardFrame, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc JailGuildServices MarketLadyLuck exchange " +
                $"{FormatSession(context with { Session = updatedSession })} " +
                $"source={LegacyJailGuildServicesMarketLadyLuckSource.SourceMethod} " +
                $"sourceLines={LegacyJailGuildServicesMarketLadyLuckSource.SourceLines} optionId={optionId} " +
                $"removedItemId={LegacyJailGuildServicesLotteryTicketItemId} removedUid={removalTarget.Uid} " +
                $"removedBound={removalTarget.Bound} rewardItemId={LegacyJailGuildServicesSmallLotteryTicketItemId} " +
                $"rewardColor={rewardItem.Color} bound=false sourceOrder=remove-item+add-item no-success-dialog=true.")
            .ConfigureAwait(false);
    }

    private static bool HasLegacyJailGuildServicesUnboundItem(
        PlayerItemSnapshot items,
        uint itemId)
    {
        var count = 0u;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != itemId || item.Bound != 0)
            {
                continue;
            }

            count += item.StackSize;
            if (count >= 1)
            {
                return true;
            }
        }

        return false;
    }

}
