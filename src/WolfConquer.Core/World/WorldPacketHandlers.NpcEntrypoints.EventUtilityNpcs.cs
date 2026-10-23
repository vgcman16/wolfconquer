using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDancePkNpcId = 6_017;
    private const uint LegacyVicotryBuddhaNpcId = 25_003;
    private const uint LegacyMarialDealerNpcId = 15_987;
    private const uint LegacySwapperStarryNpcId = 832_663;
    private const uint LegacyElementalPoolNpcId = 832_664;
    private const uint LegacyDailyItem1NpcId = uint.MaxValue - 7;
    private const long LegacyDancePkConquerPointCost = 1;
    private const uint LegacyVicotryBuddhaNecklaceItemId = 3_007_563;
    private const uint LegacyVicotryBuddhaBoundlessHeartItemId = 3_007_564;
    private const uint LegacyVicotryBuddhaRosewoodIncenseItemId = 3_007_565;
    private const uint LegacyVicotryBuddhaRatePercent = 30;
    private const int LegacyVicotryBuddhaItemTypeStackSizeFieldIndex = 46;
    private const string RuntimeLegacyVicotryBuddhaRateRollAttribute = "LegacyVicotryBuddhaRateRoll";
    private const uint LegacySwapperStarryRedFlowerItemId = 722_732;
    private const uint LegacySwapperStarryWhiteFlowerItemId = 722_736;
    private const uint LegacySwapperStarryYinYangSealItemId = 3_004_462;
    private const uint LegacyElementalPoolStardustItemId = 3_004_465;
    private const uint LegacyElementalPoolShadowScrollItemId = 3_004_459;
    private const uint LegacyElementalPoolTwilightScrollItemId = 3_004_460;
    private const uint LegacyElementalPoolFatalScrollItemId = 3_004_461;
    private const uint LegacySwapperStarryFlowerCount = 4;
    private const long LegacySwapperStarryYinYangSealCost = 1_000;
    private const uint LegacySwapperStarryRateValue = 30;
    private const uint LegacySwapperStarryRateDiscriminant = 100;
    private const string RuntimeLegacySwapperStarryRateRollAttribute = "LegacySwapperStarryRateRoll";

    public static async ValueTask<bool> TryHandleLegacyEventUtilityNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyDailyItem1NpcId => await TryHandleLegacyDailyItem1Async(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyDancePkNpcId => await TryHandleLegacyDancePkAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyVicotryBuddhaNpcId => await TryHandleLegacyVicotryBuddhaAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyMarialDealerNpcId => await TryHandleLegacyMarialDealerAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySwapperStarryNpcId => await TryHandleLegacySwapperStarryAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyElementalPoolNpcId => await TryHandleLegacyElementalPoolAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyDailyItem1Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var destination = optionId switch
        {
            1 => new LegacyNpcTeleportDestination(1_015, 746, 665, "NpcHandler.DailyItem1"),
            2 => new LegacyNpcTeleportDestination(1_002, 270, 470, "NpcHandler.DailyItem1"),
            _ => default,
        };
        if (destination.SourceMethod is null)
        {
            return false;
        }

        await SendLegacyNpcTeleportAsync(context, session, LegacyDailyItem1NpcId, optionId, destination, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacyDancePkAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.DancePK";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Hello, I am here for Dance PK."],
                        [new("Yes.", 1), new("No thanks.", LegacyNpcClosedOption)],
                        325,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.ConquerPoints < LegacyDancePkConquerPointCost)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["You do not have 1 CP with you."],
                            [new("I see.", LegacyNpcClosedOption)],
                            7,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var updatedCharacter = session.Character with
                {
                    ConquerPoints = checked(session.Character.ConquerPoints - LegacyDancePkConquerPointCost),
                };
                var updatedSession = session with { Character = updatedCharacter };
                context.Sessions?.Set(context.Connection, updatedSession);
                var updatedContext = context with { Session = updatedSession };

                await PersistLegacyDancePkConquerPointsAsync(updatedContext, updatedSession, sourceMethod)
                    .ConfigureAwait(false);
                await SendScalarUpdateAsync(
                        updatedContext,
                        updatedCharacter,
                        LegacyMsgUpdateDataType.ConquerPoints,
                        checked((ulong)updatedCharacter.ConquerPoints),
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendLegacyNpcTeleportAsync(
                        updatedContext,
                        updatedSession,
                        LegacyDancePkNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(7_357, 50, 50, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer applied LegacyNpc DancePK CP fee " +
                        $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                        $"cost={LegacyDancePkConquerPointCost.ToString(CultureInfo.InvariantCulture)} " +
                        $"sourceConquerPointCost={LegacyDancePkConquerPointCost.ToString(CultureInfo.InvariantCulture)} " +
                        $"tunedConquerPointCost={LegacyDancePkConquerPointCost.ToString(CultureInfo.InvariantCulture)} " +
                        $"updatedConquerPoints={updatedCharacter.ConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                        "walletUpdate=LegacyMsgUpdate.ConquerPoints.")
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask PersistLegacyDancePkConquerPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
        };

        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                values,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc DancePK character " +
                    $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(path)} keys=ConquerPoints.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc DancePK character persistence " +
                $"{FormatSession(context)} source={sourceMethod} reason={reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyVicotryBuddhaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.VicotryBuddha";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "The mortal world is still the best place for me. I received enlightenment here thousands of years ago.",
                            "With Buddha's Necklace, you may have a chance to achieve that during meditation.",
                        ],
                        [
                            new("Remove distractions (1 Necklace).", 1),
                            new("I want to leave here.", 3),
                            new("I prefer to keep them.", LegacyNpcClosedOption),
                        ],
                        226,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Are you sure you want to start this trial with the power of Buddha's Necklace?"],
                        [new("Yes.", 2), new("No.", LegacyNpcClosedOption)],
                        226,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await ApplyLegacyVicotryBuddhaNecklaceExchangeAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyVicotryBuddhaNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_004, 37, 36, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask ApplyLegacyVicotryBuddhaNecklaceExchangeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var necklace = session.PlayerItems.InventoryItems
            .Where(item => item.ItemId == LegacyVicotryBuddhaNecklaceItemId && item.Bound == 0 && item.StackSize > 0)
            .OrderBy(item => item.Uid)
            .FirstOrDefault();
        if (necklace is null)
        {
            if (context.WorldData is null && string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
            {
                await SendLegacyEventUtilityUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Buddha's Necklace exchange is unavailable here.",
                        "source inventory services are unavailable for the Buddha's Necklace exchange",
                        226,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Please come back when you will have 1 Buddha`sNecklace in your inventory."],
                    [new LegacyNpcDialogOption("Oh.", LegacyNpcClosedOption)],
                    226,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc VicotryBuddha necklace exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"necklaceItemId={LegacyVicotryBuddhaNecklaceItemId} reason=unbound-necklace-missing.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc VicotryBuddha necklace exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    "reason=session-store-missing; no item persistence or inventory mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc VicotryBuddha necklace exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    "reason=playerItemsRoot-missing; no inventory mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        var rateRoll = TryReadUInt32Attribute(
                session.Character.Attributes,
                out var forcedRateRoll,
                RuntimeLegacyVicotryBuddhaRateRollAttribute)
            ? forcedRateRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        var rateHit = LegacyVicotryBuddhaRatePercent > rateRoll;
        var rewardItemId = rateHit
            ? LegacyVicotryBuddhaBoundlessHeartItemId
            : LegacyVicotryBuddhaRosewoodIncenseItemId;
        var rewardMessage = rateHit
            ? "You received BoundlessHeart."
            : "You received RosewoodIncense.";

        if (!TryReadLegacyItemType(context.WorldData, rewardItemId, out var rewardItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc VicotryBuddha necklace exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"rewardItemId={rewardItemId} rateRoll={rateRoll} reason=itemtype-missing; " +
                    "no necklace was consumed.")
                .ConfigureAwait(false);
            return;
        }

        if (rewardItemType.Id != checked((int)rewardItemId) ||
            rewardItemType.LegacyDurability > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc VicotryBuddha necklace exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"rewardItemId={rewardItemId} rateRoll={rateRoll} reason=itemtype-invalid; " +
                    "no necklace was consumed.")
                .ConfigureAwait(false);
            return;
        }

        var consumedItems = ConsumeSingleLegacyInventoryItem(
            session.PlayerItems,
            necklace,
            out var consumeFrame);
        var rewardStackLimit = TryReadLegacyVicotryBuddhaStackLimit(rewardItemType.Fields);
        var compatibleStack = rewardStackLimit > 0
            ? consumedItems.InventoryItems.FirstOrDefault(item =>
                item.ItemId == rewardItemId &&
                item.Bound == 0 &&
                (uint)item.StackSize + 1 <= rewardStackLimit)
            : null;

        PlayerItemSnapshotItem rewardItem;
        PlayerItemSnapshot updatedItems;
        ushort rewardItemMode;
        if (compatibleStack is not null)
        {
            rewardItem = compatibleStack.WithStackSize(checked((ushort)(compatibleStack.StackSize + 1)));
            updatedItems = ReplaceInventoryItem(consumedItems, compatibleStack, rewardItem);
            rewardItemMode = LegacyGameItemPacket.UpdateItemMode;
        }
        else
        {
            if (!HasLegacyInventorySpace(consumedItems, 1))
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc VicotryBuddha necklace exchange " +
                        $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                        $"rewardItemId={rewardItemId} rateRoll={rateRoll} reason=inventory-full; " +
                        "no necklace was consumed.")
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                rewardItem = CreatePurchasedItems(consumedItems, rewardItemType, count: 1, isBound: false)[0];
            }
            catch (OverflowException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc VicotryBuddha necklace exchange " +
                        $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                        $"rewardItemId={rewardItemId} rateRoll={rateRoll} reason=item-uid-exhausted; " +
                        "no necklace was consumed.")
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = consumedItems with
            {
                InventoryItems = consumedItems.InventoryItems
                    .Concat([rewardItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
            rewardItemMode = LegacyGameItemPacket.AddItemMode;
        }

        var updatedSession = session with { PlayerItems = updatedItems };
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
                    "PARITY-GAP LegacyNpc VicotryBuddha necklace exchange persistence " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"rewardItemId={rewardItemId} reason={ex.GetType().Name}; " +
                    "no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(rewardMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: rewardItemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc VicotryBuddha necklace exchange " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"necklaceItemId={LegacyVicotryBuddhaNecklaceItemId} rewardItemId={rewardItemId} " +
                $"Rate({LegacyVicotryBuddhaRatePercent})={rateHit.ToString().ToLowerInvariant()} " +
                $"rateRoll={rateRoll} bound=false " +
                $"itemMode={(rewardItemMode == LegacyGameItemPacket.UpdateItemMode ? "update" : "add")}.")
            .ConfigureAwait(false);
    }

    private static ushort TryReadLegacyVicotryBuddhaStackLimit(IReadOnlyList<string> fields)
    {
        return fields.Count > LegacyVicotryBuddhaItemTypeStackSizeFieldIndex &&
            ushort.TryParse(
                fields[LegacyVicotryBuddhaItemTypeStackSizeFieldIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var stackLimit)
            ? stackLimit
            : (ushort)0;
    }

    private static async ValueTask<bool> TryHandleLegacySwapperStarryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.SwapperStarry";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "You need 4 Red Flowers and 4 White Flowers to begin the quest. You can obtain them in the Evil Abyss.",
                            "There are 3 stages: feel the Yin Yang Stele, awaken the Elemental Guard, then try the Wheel of Life.",
                        ],
                        [
                            new("Tell me more.", 1),
                            new("I have 4 Red Flowers and 4 White Flowers.", 3),
                            new("Buy Yin Yang Seal for 1000 CPs.", 12),
                            new("Teleport to Evil Abyss.", 2),
                            new("Teleport to Twin City.", 10),
                            new("Not interested.", LegacyNpcClosedOption),
                        ],
                        203,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "If you fail during Stage 2 or Stage 3, you receive Stardust instead.",
                            "Stardust can be exchanged at Infinite Abyss for Shadow Scroll, Twilight Scroll, or Fatal Scroll.",
                            "Life's Eye can transform an equipped Katana into Nobunaga's Claw.",
                        ],
                        [new("Sounds good.", LegacyNpcClosedOption)],
                        203,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacySwapperStarryNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_700, 612, 634, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacySwapperStarryNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await ApplyLegacySwapperStarryFlowerExchangeAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 12:
                await ApplyLegacySwapperStarryYinYangSealPurchaseAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyElementalPoolAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ElementalPool";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [
                            "I can open Nightmare Captain for one Yin Yang Seal, open Purple Banshee for one Elemental Jade, or exchange Stardust for scrolls.",
                        ],
                        [
                            new("Change my Stardust.", 1),
                            new("Open Nightmare Captain.", 3),
                            new("Open Purple Banshee.", 2),
                            new("Not interested.", LegacyNpcClosedOption),
                        ],
                        203,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["I exchange Stardust for Shadow Scroll, Twilight Scroll, or Fatal Scroll. Please select an item."],
                        [
                            new("Shadow Scroll.", 10),
                            new("Twilight Scroll.", 11),
                            new("Fatal Scroll.", 12),
                            new("Not interested.", LegacyNpcClosedOption),
                        ],
                        203,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
            case 3:
                await SendLegacyEventUtilityUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Elemental monster opening is unavailable here.",
                        optionId switch
                        {
                            2 => "source removes item 3004463 and spawns monster 20101 on map 3851",
                            3 => "source removes item 3004462 and spawns monster 20100 on map 3851",
                            _ => throw new InvalidOperationException("Unexpected ElementalPool monster option."),
                        },
                        203,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
            case 11:
            case 12:
                await ApplyLegacyElementalPoolStardustExchangeAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case >= 4 and <= 9:
                await context.Log.WriteLineAsync(
                        "WorldServer ignored LegacyNpc ElementalPool source no-op " +
                        $"{FormatSession(context)} source={sourceMethod} option={optionId}.")
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask ApplyLegacySwapperStarryFlowerExchangeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyEventUtilityUnboundItemCount(
                session.PlayerItems,
                LegacySwapperStarryRedFlowerItemId,
                LegacySwapperStarryFlowerCount) ||
            !HasLegacyEventUtilityUnboundItemCount(
                session.PlayerItems,
                LegacySwapperStarryWhiteFlowerItemId,
                LegacySwapperStarryFlowerCount))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Please come back when you will have at inventory 4 Red Flowers and 4 White Flowers."],
                    [new LegacyNpcDialogOption("Ah~OK", LegacyNpcClosedOption)],
                    203,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetLegacyEventUtilityMutationInfrastructure(
                context,
                session,
                requireUsersRoot: false,
                out var sessions,
                out var playerItemsRoot,
                out var infrastructureReason))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    infrastructureReason + "; no flowers were consumed")
                .ConfigureAwait(false);
            return;
        }

        var mutationFrames = new List<byte[]>();
        if (!TryConsumeLegacyEventUtilityEligibleItems(
                session.PlayerItems,
                LegacySwapperStarryRedFlowerItemId,
                LegacySwapperStarryFlowerCount,
                mutationFrames,
                out var updatedItems) ||
            !TryConsumeLegacyEventUtilityEligibleItems(
                updatedItems,
                LegacySwapperStarryWhiteFlowerItemId,
                LegacySwapperStarryFlowerCount,
                mutationFrames,
                out updatedItems))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "source removal count drifted after the unbound Contain gates; no flowers were consumed")
                .ConfigureAwait(false);
            return;
        }

        var rateRoll = TryReadUInt32Attribute(
                session.Character.Attributes,
                out var forcedRateRoll,
                RuntimeLegacySwapperStarryRateRollAttribute)
            ? forcedRateRoll % LegacySwapperStarryRateDiscriminant
            : checked((uint)Random.Shared.Next(0, checked((int)LegacySwapperStarryRateDiscriminant)));
        var rateHit = LegacySwapperStarryRateValue > rateRoll;

        PlayerItemSnapshotItem? rewardItem = null;
        if (rateHit)
        {
            if (!HasLegacyInventorySpace(updatedItems, 1))
            {
                await LogLegacyEventUtilityGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Rate(30) hit but the source Add would fail after eligible flower removal because inventory is full; no flowers were consumed")
                    .ConfigureAwait(false);
                return;
            }

            if (!TryCreateLegacyEventUtilityRewardItem(
                    context.WorldData,
                    session.PlayerItems,
                    LegacySwapperStarryYinYangSealItemId,
                    out rewardItem,
                    out var rewardReason))
            {
                await LogLegacyEventUtilityGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        rewardReason + "; no flowers were consumed")
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems
                    .Concat([rewardItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
        }

        var updatedSession = session with { PlayerItems = updatedItems };
        if (!await TryPersistLegacyEventUtilityItemsAsync(
                context,
                updatedSession,
                playerItemsRoot,
                sourceMethod,
                optionId,
                "flower exchange")
            .ConfigureAwait(false))
        {
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        foreach (var frame in mutationFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        if (rewardItem is not null)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket
                        .FromSnapshotItem(rewardItem, mode: LegacyGameItemPacket.AddItemMode)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await SendLegacyNpcDialogAsync(
                    updatedContext,
                    sourceMethod,
                    ["You wasn`t very lucky today .. I hope you will have a better luck next time ^^."],
                    [new LegacyNpcDialogOption("Ah~OK", LegacyNpcClosedOption)],
                    203,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc SwapperStarry flower exchange " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"redFlowerItemId={LegacySwapperStarryRedFlowerItemId} redCount={LegacySwapperStarryFlowerCount} " +
                $"whiteFlowerItemId={LegacySwapperStarryWhiteFlowerItemId} whiteCount={LegacySwapperStarryFlowerCount} " +
                $"Rate(30)={rateHit.ToString().ToLowerInvariant()} rateRoll={rateRoll} " +
                $"rewardItemId={(rewardItem?.ItemId ?? 0).ToString(CultureInfo.InvariantCulture)} " +
                "sourceOrder=RemoveRed,RemoveWhite,Rate,RewardOrFailureDialog.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacySwapperStarryYinYangSealPurchaseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (session.Character.ConquerPoints < LegacySwapperStarryYinYangSealCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Sorry, but you don`t have enough CPs to buy that."],
                    [new LegacyNpcDialogOption("Alright.", LegacyNpcClosedOption)],
                    7,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (session.LegacyTradeActive)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc SwapperStarry Yin Yang Seal purchase " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    "reason=in-trade; source branch is silent.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Please make 1 more space in your inventory"],
                    [new LegacyNpcDialogOption("Let me check.", LegacyNpcClosedOption)],
                    203,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetLegacyEventUtilityMutationInfrastructure(
                context,
                session,
                requireUsersRoot: true,
                out var sessions,
                out var playerItemsRoot,
                out var infrastructureReason))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    infrastructureReason + "; no CPs or items were mutated")
                .ConfigureAwait(false);
            return;
        }

        if (!TryCreateLegacyEventUtilityRewardItem(
                context.WorldData,
                session.PlayerItems,
                LegacySwapperStarryYinYangSealItemId,
                out var rewardItem,
                out var rewardReason))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    rewardReason + "; no CPs or items were mutated")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(
            session.Character.ConquerPoints - LegacySwapperStarryYinYangSealCost);
        var updatedCharacter = session.Character with { ConquerPoints = updatedConquerPoints };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };

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
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"Yin Yang Seal character persistence preflight failed reason={ex.GetType().Name}; no CPs or items were mutated")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotLoader itemLoader;
        try
        {
            itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
            itemLoader.SaveByCharacterUid(updatedCharacter.Uid, updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"Yin Yang Seal item persistence failed reason={ex.GetType().Name}; no session mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        string characterPath;
        string characterReason;
        bool characterPersisted;
        try
        {
            characterPersisted = TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                out characterPath,
                out characterReason);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            characterPersisted = false;
            characterPath = string.Empty;
            characterReason = ex.GetType().Name;
        }

        if (!characterPersisted)
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

            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"Yin Yang Seal character persistence failed reason={characterReason} " +
                    $"itemRollback={itemRollbackReason} characterRollback={characterRollbackReason}; " +
                    "no session mutation was committed")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(rewardItem, mode: LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc SwapperStarry Yin Yang Seal purchase " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"cost={LegacySwapperStarryYinYangSealCost} updatedConquerPoints={updatedConquerPoints} " +
                $"rewardItemId={rewardItem.ItemId} rewardItemUid={rewardItem.Uid} bound={rewardItem.Bound} " +
                $"characterPath={Path.GetFileName(characterPath)} sourceOrder=ConquerPoints,AddItem.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacyElementalPoolStardustExchangeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var rewardItemId = optionId switch
        {
            10 => LegacyElementalPoolShadowScrollItemId,
            11 => LegacyElementalPoolTwilightScrollItemId,
            12 => LegacyElementalPoolFatalScrollItemId,
            _ => throw new InvalidOperationException("Unexpected ElementalPool Stardust option."),
        };

        if (!HasLegacyEventUtilityUnboundItemCount(
                session.PlayerItems,
                LegacyElementalPoolStardustItemId,
                1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Sorry, you not have the Stardust in your inventory."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    203,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!TryGetLegacyEventUtilityMutationInfrastructure(
                context,
                session,
                requireUsersRoot: false,
                out var sessions,
                out var playerItemsRoot,
                out var infrastructureReason))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    infrastructureReason + "; no Stardust was consumed")
                .ConfigureAwait(false);
            return;
        }

        var mutationFrames = new List<byte[]>(1);
        if (!TryConsumeLegacyEventUtilityEligibleItems(
                session.PlayerItems,
                LegacyElementalPoolStardustItemId,
                1,
                mutationFrames,
                out var updatedItems))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "source Stardust removal drifted after the unbound Contain gate; no Stardust was consumed")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventorySpace(updatedItems, 1))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "source Add would fail after eligible Stardust removal because inventory is full; no Stardust was consumed")
                .ConfigureAwait(false);
            return;
        }

        if (!TryCreateLegacyEventUtilityRewardItem(
                context.WorldData,
                session.PlayerItems,
                rewardItemId,
                out var rewardItem,
                out var rewardReason))
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    rewardReason + "; no Stardust was consumed")
                .ConfigureAwait(false);
            return;
        }

        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems
                .Concat([rewardItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        if (!await TryPersistLegacyEventUtilityItemsAsync(
                context,
                updatedSession,
                playerItemsRoot,
                sourceMethod,
                optionId,
                "Stardust exchange")
            .ConfigureAwait(false))
        {
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(mutationFrames[0], cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(rewardItem, mode: LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc ElementalPool Stardust exchange " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"stardustItemId={LegacyElementalPoolStardustItemId} rewardItemId={rewardItemId} " +
                $"rewardItemUid={rewardItem.Uid} bound={rewardItem.Bound} sourceOrder=Remove,Add.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyEventUtilityMutationInfrastructure(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool requireUsersRoot,
        out WorldSessionStore sessions,
        out string playerItemsRoot,
        out string reason)
    {
        sessions = null!;
        playerItemsRoot = string.Empty;
        reason = string.Empty;

        if (context.Sessions is not { } availableSessions)
        {
            reason = "session-store-missing";
            return false;
        }

        if (!availableSessions.TryGet(context.Connection, out var registeredSession) ||
            registeredSession is null)
        {
            reason = "session-registration-missing";
            return false;
        }

        if (session.Character.Uid <= 0)
        {
            reason = "character-uid-unavailable";
            return false;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            reason = "player-items-root-missing";
            return false;
        }

        if (requireUsersRoot && string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            reason = "users-root-missing";
            return false;
        }

        sessions = availableSessions;
        playerItemsRoot = context.PlayerItemsRoot;
        return true;
    }

    private static bool HasLegacyEventUtilityUnboundItemCount(
        PlayerItemSnapshot items,
        uint itemId,
        uint count)
    {
        ulong available = 0;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != itemId || item.Bound != 0 || item.StackSize == 0)
            {
                continue;
            }

            available = checked(available + item.StackSize);
            if (available >= count)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConsumeLegacyEventUtilityEligibleItems(
        PlayerItemSnapshot items,
        uint itemId,
        uint count,
        List<byte[]> mutationFrames,
        out PlayerItemSnapshot updatedItems)
    {
        updatedItems = items;
        for (var removed = 0U; removed < count; removed++)
        {
            var item = updatedItems.InventoryItems
                .Where(candidate => candidate.ItemId == itemId && candidate.Bound == 0 && candidate.StackSize > 0)
                .OrderBy(candidate => candidate.Uid)
                .FirstOrDefault();
            if (item is null)
            {
                return false;
            }

            updatedItems = ConsumeSingleLegacyInventoryItem(updatedItems, item, out var frame);
            mutationFrames.Add(frame);
        }

        return true;
    }

    private static bool TryCreateLegacyEventUtilityRewardItem(
        WorldDataCatalog? worldData,
        PlayerItemSnapshot uidAllocationItems,
        uint itemId,
        out PlayerItemSnapshotItem rewardItem,
        out string reason)
    {
        rewardItem = null!;
        reason = string.Empty;
        if (!TryReadLegacyItemType(worldData, itemId, out var itemType))
        {
            reason = $"reward-itemtype-missing:itemId={itemId}";
            return false;
        }

        if (itemType.Id != checked((int)itemId) || itemType.LegacyDurability > ushort.MaxValue)
        {
            reason = $"reward-itemtype-invalid:itemId={itemId}";
            return false;
        }

        try
        {
            rewardItem = CreatePurchasedItems(
                    uidAllocationItems,
                    itemType,
                    count: 1,
                    isBound: false)[0]
                .WithColor(checked((uint)Random.Shared.Next(3, 9)));
            return true;
        }
        catch (OverflowException)
        {
            reason = $"item-uid-exhausted:itemId={itemId}";
            return false;
        }
    }

    private static async ValueTask<bool> TryPersistLegacyEventUtilityItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        string playerItemsRoot,
        string sourceMethod,
        byte optionId,
        string operation)
    {
        try
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedSession.PlayerItems);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await LogLegacyEventUtilityGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"{operation} persistence failed reason={ex.GetType().Name}; no session mutation was committed")
                .ConfigureAwait(false);
            return false;
        }
    }

    private static async ValueTask SendLegacyEventUtilityUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string text,
        string gapReason,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                [new LegacyNpcDialogOption("I understand.", LegacyNpcClosedOption)],
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyEventUtilityGapAsync(context, sourceMethod, optionId, gapReason).ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyEventUtilityGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc EventUtilityNpcs " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }
}
