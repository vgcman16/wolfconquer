using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyFindTheSpyNpcId = 126_789;
    private const uint LegacyBotJailNpcId = 9_176;
    private const uint LegacyCrazyWarNpcId = 101_010;
    private const uint LegacyWinCrazyWarNpcId = 101_011;
    private const uint LegacyLettersNpcId = 101_012;
    private const long LegacyCrazyWarMapId = 10_599;
    private const long LegacyLettersConquerPointsReward = 2_000_000;
    private const long LegacyLettersConquerPointsInventoryCap = 2_000_000_000;
    private const ushort LegacyMiscWarEventAvatarId = 63;
    private const ushort LegacyFindTheSpyAvatarId = 186;
    private const ushort LegacyBotJailAvatarId = 213;
    private const ushort LegacyBotJailCountAvatarId = 215;
    private const ushort LegacyLettersAvatarId = 105;

    private static readonly LegacyNpcDialogOption[] LegacyFindTheSpyOpenOptions =
    [
        new("Yes.", 1),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyFindTheSpyClosedOptions =
    [
        new("I will come back later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyBotJailOptions =
    [
        new("I am sorry. Please send me to Twin City.", 3),
        new("How many times have I entered?", 4),
        new("Sorry, I am poor.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCrazyWarOptions =
    [
        new("Yes, please.", 26),
        new("No, thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyWinCrazyWarOptions =
    [
        new("Give me the prize.", 26),
        new("No, thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyMiscWarEventOkayOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    private static readonly uint[] LegacyLettersConquer14ItemIds =
    [
        3_303_130,
        3_303_131,
        3_303_124,
        3_303_125,
        3_303_126,
        3_303_127,
        3_303_128,
        3_303_129,
    ];

    internal static ValueTask<bool> TryHandleLegacyMiscWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return TryHandleLegacyMiscWarEventNpcAsync(
            context,
            session,
            npcId,
            optionId,
            DateTime.Now,
            cancellationToken);
    }

    internal static async ValueTask<bool> TryHandleLegacyMiscWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyFindTheSpyNpcId => await TryHandleLegacyFindTheSpyAsync(
                    context,
                    session,
                    optionId,
                    now,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyBotJailNpcId => await TryHandleLegacyBotJailAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            LegacyCrazyWarNpcId => await TryHandleLegacyCrazyWarAsync(
                    context,
                    session,
                    optionId,
                    now,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyWinCrazyWarNpcId => await TryHandleLegacyWinCrazyWarAsync(
                    context,
                    session,
                    optionId,
                    now,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyLettersNpcId => await TryHandleLegacyLettersAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyFindTheSpyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.FindTheSpy";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        $"Hello, {session.Character.Name}. Do you want to enter Find the Spy?",
                        "I can help during 12:00-17:30 and 21:00-23:59.",
                    ],
                    IsLegacyFindTheSpyOpen(now) ? LegacyFindTheSpyOpenOptions : LegacyFindTheSpyClosedOptions,
                    LegacyFindTheSpyAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (IsLegacyFindTheSpyOpen(now))
            {
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyFindTheSpyNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(3_845, 172, 147, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await SendLegacyMiscWarEventUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Find the Spy is closed right now.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyBotJailAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.BotJail";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Welcome to Bot Jail #10."],
                        LegacyBotJailOptions,
                        LegacyBotJailAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                var releaseBotJailCount = ReadLegacyBotJailCount(session);
                var requiredConquerPoints = LegacyBotJailReleaseCost(releaseBotJailCount);
                if (session.Character.ConquerPoints < requiredConquerPoints)
                {
                    await context.Connection.SendAsync(
                            LegacyNpcReplyPacket.ForMessageBox("sorry you not have enough ConquerPoints  .")
                                .ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer handled LegacyNpc BotJail insufficient CP release " +
                            $"{FormatSession(context)} source={sourceMethod} sourceLines=NpcHandler.cs:7198-7266 " +
                            $"botjail={releaseBotJailCount.ToString(CultureInfo.InvariantCulture)} " +
                            $"requiredCp={requiredConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                            $"conquerPoints={session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture)}.")
                        .ConfigureAwait(false);
                    return true;
                }

                await TryApplyLegacyBotJailReleaseAsync(
                        context,
                        session,
                        sourceMethod,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                if (TryReadUInt32Attribute(session.Character.Attributes, out var botJailCount, "botjail", "BotJail", "BotJailCount"))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            [$"You have entered Bot Jail {botJailCount.ToString(CultureInfo.InvariantCulture)} time(s). Good luck."],
                            LegacyMiscWarEventOkayOptions,
                            LegacyBotJailCountAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendLegacyMiscWarEventGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Player.botjail counter is not represented in local character attributes.")
                        .ConfigureAwait(false);
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["Your Bot Jail count is not available on this server yet."],
                            LegacyMiscWarEventOkayOptions,
                            LegacyBotJailCountAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyCrazyWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.CrazyWar";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [$"Hey, {session.Character.Name}, do you want to join Crazy War? It starts every hour at xx:01."],
                    LegacyCrazyWarOptions,
                    LegacyMiscWarEventAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 26)
        {
            if (IsLegacyCrazyWarEntryOpen(now))
            {
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyCrazyWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(LegacyCrazyWarMapId, 160, 170, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await SendLegacyMiscWarEventUnavailableAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Crazy War has not started yet.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }

        return false;
    }

    private static uint ReadLegacyBotJailCount(WorldSession session)
    {
        return TryReadUInt32Attribute(session.Character.Attributes, out var botJailCount, "botjail", "BotJail", "BotJailCount")
            ? botJailCount
            : 0;
    }

    private static long LegacyBotJailReleaseCost(uint botJailCount)
    {
        return botJailCount switch
        {
            0 or 1 => 25_000_000,
            2 => 30_000_000,
            _ => 50_000_000,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyWinCrazyWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.WinCrazyWar";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [$"Hey, {session.Character.Name}, do you want to claim the Crazy War prize?"],
                    LegacyWinCrazyWarOptions,
                    LegacyMiscWarEventAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 26)
        {
            if (!IsLegacyCrazyWarRewardOpen(now))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["Crazy War rewards are available from xx:06 through xx:30."],
                        [new LegacyNpcDialogOption("Thanks.", 58)],
                        LegacyMiscWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var alivePlayers = CountLegacyCrazyWarAlivePlayers(context, session);
            if (alivePlayers == 1)
            {
                await SendLegacyMiscWarEventGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        "Single live Crazy War participant was locally proven, but source reward grants inventory items, study points, inner power potency, class experience, 500,000 CP, and a center broadcast; this slice only preserves the source return teleport.")
                    .ConfigureAwait(false);
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyWinCrazyWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 355, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await SendLegacyMiscWarEventGapAsync(
                        context,
                        sourceMethod,
                        optionId,
                        $"Source requires exactly one live player on map 10599; local count was {alivePlayers.ToString(CultureInfo.InvariantCulture)}.")
                    .ConfigureAwait(false);
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["The Crazy War prize is not available yet."],
                        LegacyMiscWarEventOkayOptions,
                        LegacyMiscWarEventAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyLettersAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.Letters";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["This NPC feature is unavailable for now. Please wait for a GM announcement."],
                        [new LegacyNpcDialogOption("Sorry to bother you.", LegacyNpcClosedOption)],
                        LegacyLettersAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (ReadAttributeUInt32(session.Character.Attributes, "Coonquer14", "Conquer14", "LettersConquer14") != 0)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["You can only do this once per day."],
                            LegacyMiscWarEventOkayOptions,
                            LegacyLettersAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (!HasLegacyLettersConquer14Items(session))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            sourceMethod,
                            ["You do not have all letters for C,O,N,Q,U,E,R,(14), or this exchange is unavailable here."],
                            LegacyMiscWarEventOkayOptions,
                            LegacyLettersAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (context.Sessions is not { } sessions)
                {
                    await SendLegacyMiscWarEventGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Letters option 1 session store is missing; no character or item mutation was persisted.")
                        .ConfigureAwait(false);
                    return true;
                }

                if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
                {
                    await SendLegacyMiscWarEventGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Letters option 1 PlayerItemsRoot is missing; no character or item mutation was persisted.")
                        .ConfigureAwait(false);
                    return true;
                }

                if (string.IsNullOrWhiteSpace(session.UsersRoot))
                {
                    await SendLegacyMiscWarEventGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Letters option 1 UsersRoot is missing; no character or item mutation was persisted.")
                        .ConfigureAwait(false);
                    return true;
                }

                var updatedItems = session.PlayerItems;
                var removalFrames = new List<byte[]>(LegacyLettersConquer14ItemIds.Length);
                foreach (var itemId in LegacyLettersConquer14ItemIds)
                {
                    updatedItems = RemoveLegacyLettersConquer14Item(updatedItems, itemId, out var removalFrame);
                    removalFrames.Add(removalFrame);
                }

                var rewardedConquerPoints = checked(
                    session.Character.ConquerPoints + LegacyLettersConquerPointsReward);
                var overflowConquerPoints = Math.Max(
                    0,
                    rewardedConquerPoints - LegacyLettersConquerPointsInventoryCap);
                var updatedConquerPoints = rewardedConquerPoints - overflowConquerPoints;
                var updatedBankConquerPoints = checked(
                    session.Character.BankConquerPoints + overflowConquerPoints);
                var updatedCharacter = session.Character with
                {
                    ConquerPoints = updatedConquerPoints,
                    BankConquerPoints = updatedBankConquerPoints,
                };
                updatedCharacter = WithCharacterAttribute(
                    updatedCharacter,
                    "ConquerPoints",
                    checked((ulong)updatedConquerPoints));
                updatedCharacter = WithCharacterAttribute(
                    updatedCharacter,
                    "BankCps",
                    checked((ulong)updatedBankConquerPoints));
                updatedCharacter = WithCharacterAttribute(updatedCharacter, "Coonquer14", 1UL);
                var updatedSession = session with
                {
                    Character = updatedCharacter,
                    PlayerItems = updatedItems,
                };

                PlayerItemSnapshotLoader itemLoader;
                try
                {
                    itemLoader = new PlayerItemSnapshotLoader(context.PlayerItemsRoot);
                    itemLoader.SaveByCharacterUid(updatedCharacter.Uid, updatedItems);
                }
                catch (Exception ex) when (
                    ex is IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException)
                {
                    await SendLegacyMiscWarEventGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            $"Letters option 1 item persistence failed; reason={ex.GetType().Name}; no session mutation was committed.")
                        .ConfigureAwait(false);
                    return true;
                }

                var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
                    ["BankCps"] = updatedBankConquerPoints.ToString(CultureInfo.InvariantCulture),
                    ["Coonquer14"] = "1",
                };
                if (!TryPersistLegacyCharacterAttributes(
                        session.UsersRoot,
                        session.AccountId,
                        updatedCharacter.Uid,
                        persistenceValues,
                        out var characterPath,
                        out var persistenceReason))
                {
                    var rollbackReason = "none";
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
                        rollbackReason = ex.GetType().Name;
                    }

                    await SendLegacyMiscWarEventGapAsync(
                            context,
                            sourceMethod,
                            optionId,
                            "Letters option 1 character persistence failed; " +
                            $"reason={persistenceReason}; itemRollback={rollbackReason}; no session mutation was committed.")
                        .ConfigureAwait(false);
                    return true;
                }

                sessions.Set(context.Connection, updatedSession);
                var updatedContext = context with { Session = updatedSession };

                foreach (var removalFrame in removalFrames)
                {
                    await context.Connection.SendAsync(removalFrame, cancellationToken).ConfigureAwait(false);
                }

                if (overflowConquerPoints > 0)
                {
                    await context.Connection.SendAsync(
                            LegacyNpcReplyPacket
                                .ForMessageBox(
                                    $"{overflowConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                                    "Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.")
                                .ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer applied LegacyNpc Letters Conquer14 exchange " +
                        $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                        $"itemIds={string.Join(',', LegacyLettersConquer14ItemIds)} " +
                        $"rewardConquerPoints={LegacyLettersConquerPointsReward} " +
                        $"conquerPoints={updatedConquerPoints} bankCps={updatedBankConquerPoints} " +
                        $"flag=Coonquer14:1 characterPath={Path.GetFileName(characterPath)}.")
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static bool IsLegacyFindTheSpyOpen(DateTime now)
    {
        return now.Hour is >= 12 and <= 16 ||
            now.Hour == 17 && now.Minute <= 30 ||
            now.Hour is >= 21 and <= 23;
    }

    private static bool IsLegacyCrazyWarEntryOpen(DateTime now)
    {
        return now.Minute is >= 1 and <= 5;
    }

    private static bool IsLegacyCrazyWarRewardOpen(DateTime now)
    {
        return now.Minute is >= 6 and <= 30;
    }

    private static int CountLegacyCrazyWarAlivePlayers(WorldPacketHandlerContext context, WorldSession currentSession)
    {
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is not { Count: > 0 })
        {
            return IsLegacyCrazyWarAliveParticipant(currentSession) ? 1 : 0;
        }

        return activeSessions.Count(static active => IsLegacyCrazyWarAliveParticipant(active.Session));
    }

    private static bool IsLegacyCrazyWarAliveParticipant(WorldSession session)
    {
        return session.Character.MapId == LegacyCrazyWarMapId && session.Character.MinHitPoints >= 1;
    }

    private static bool HasLegacyLettersConquer14Items(WorldSession session)
    {
        return LegacyLettersConquer14ItemIds.All(itemId => HasInventoryItem(session, itemId));
    }

    private static bool HasInventoryItem(WorldSession session, uint itemId)
    {
        return session.PlayerItems.InventoryItems.Any(
            item => item.ItemId == itemId && item.Bound == 0 && item.StackSize >= 1);
    }

    private static PlayerItemSnapshot RemoveLegacyLettersConquer14Item(
        PlayerItemSnapshot items,
        uint itemId,
        out byte[] frame)
    {
        var inventoryItems = items.InventoryItems.ToList();
        var index = inventoryItems.FindIndex(item => item.ItemId == itemId);
        var item = inventoryItems[index];
        if (item.StackSize > 1 && item.Position < 40)
        {
            var updatedItem = item.WithStackSize(checked((ushort)(item.StackSize - 1)));
            inventoryItems[index] = updatedItem;
            frame = LegacyGameItemPacket
                .FromSnapshotItem(updatedItem, mode: LegacyGameItemPacket.UpdateItemMode)
                .ToFrameBytes();
        }
        else
        {
            inventoryItems.RemoveAt(index);
            frame = new MsgItemUsagePacket(
                item.Uid,
                DwParam: 0,
                MsgItemUsageAction.RemoveInventory,
                Timestamp: 0,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0).ToFrameBytes();
        }

        return items with { InventoryItems = inventoryItems.ToArray() };
    }

    private static async ValueTask SendLegacyMiscWarEventUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string text,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [text],
                LegacyMiscWarEventOkayOptions,
                LegacyMiscWarEventAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc MiscWarEvent unavailable dialog " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyMiscWarEventGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc MiscWarEvent " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
    }
}
