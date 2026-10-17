using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyEventsInHourNpcId = 5_595;
    private const long LegacyHourlyEventSourcePrizeConquerPoints = 500_000;
    private const long TunedLegacyHourlyEventPrizeConquerPoints = 50_000;
    private const uint LegacyHourlyEventsArrestTokenItemId = 711_624;
    private const ushort LegacyHourlyEventsArrestTokenExchangeCount = 1;
    private const ushort LegacyHourlyEventsRandomRelicTokenExchangeCount = 3;
    private const long LegacyHourlyEventsArrestTokenConquerPoints = 200_000;
    private const ushort LegacyHourlyEventsClassExperienceTokenExchangeCount = 5;
    private const ulong LegacyHourlyEventsClassExperienceReward = 500;
    private const ulong LegacyHourlyEventsClassExperienceDialogReward = 5_000;
    private const ushort LegacyHourlyEventsMenuAvatarId = 277;
    private const ushort LegacyHourlyEventsSaturnAvatarId = 280;
    private const ushort LegacyHourlyEventsUnavailableAvatarId = 260;
    private const ushort LegacyHourlyEventsExitAvatarId = 154;
    private const ushort LegacyHourlyEventsEndAvatarId = 63;

    private static readonly LegacyNpcDialogOption[] LegacyHourlyEventsSaturnInfoOptions =
    [
        new("Let me in.", 45),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHourlyEventsSaturnTokenOptions =
    [
        new("200,000 CPs [1 token]", 107),
        new("1 random relic [3 tokens]", 106),
        new("500 Divine Class Experience [5 tokens]", 109),
        new("5 red rune fragments [10 tokens]", 111),
        new("5 blue rune fragments [10 tokens]", 112),
        new("5 yellow rune fragments [10 tokens]", 113),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHourlyEventsUnavailableOptions =
    [
        new("Got it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHourlyEventsExitOptions =
    [
        new("Yes", 1),
        new("Teleport to Twin City.", 2),
        new("Ah, wait.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHourlyEventsExitConfirmOptions =
    [
        new("Yes", 3),
        new("Ah, no.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyHourlyEventsEndOptions =
    [
        new("Thanks", 58),
    ];

    private static readonly IReadOnlyDictionary<byte, LegacyHourlyEventEntry> LegacyHourlyEventEntries =
        new Dictionary<byte, LegacyHourlyEventEntry>
        {
            [1] = new(1, 2, 4, "Tomb [Available]", 10_770, 50, 50),
            [2] = new(2, 5, 5, "Skill Tournament [Available]", 0, 0, 0),
            [3] = new(3, 8, 10, "Server Hero [Available]", 10_298, 50, 50),
            [4] = new(4, 11, 13, "Dragon War [Available]", 10_762, 50, 50),
            [5] = new(5, 14, 17, "Elite Killer [Available]", 10_761, 50, 50),
            [6] = new(6, 18, 21, "Last Man Standing [Available]", 10_300, 50, 50),
            [7] = new(7, 22, 25, "Server Captain [Available]", 10_760, 50, 50),
            [8] = new(8, 26, 30, "Ragers [Available]", 10_593, 105, 87),
            [9] = new(9, 31, 34, "Top Fight [Available]", 10_763, 50, 50),
            [10] = new(10, 35, 37, "Death Match [Available]", 10_299, 50, 50),
            [11] = new(11, 38, 40, "Fast Match [Available]", 10_079, 80, 60),
            [12] = new(12, 41, 43, "Old Man [Available]", 3_872, 50, 50),
            [13] = new(13, 44, 47, "Wave Blade [Available]", 10_759, 38, 40),
            [14] = new(14, 50, 55, "PK-PK [Available]", 10_301, 50, 50),
        };

    private static readonly IReadOnlyDictionary<uint, LegacyHourlyEventExit> LegacyHourlyEventExits =
        new Dictionary<uint, LegacyHourlyEventExit>
        {
            [199_151] = new(199_151, "NpcHandler.EventOutxx5", 10_770, 5, 30, "xx:5"),
            [199_152] = new(199_152, "NpcHandler.EventOutxx8", 10_298, 11, 30, "xx:8"),
            [199_153] = new(199_153, "NpcHandler.EventOutxx11", 10_762, 14, 30, "xx:11"),
            [199_154] = new(199_154, "NpcHandler.EventOutxx14", 10_761, 18, 30, "xx:14"),
            [199_155] = new(199_155, "NpcHandler.EventOutxx18", 10_300, 22, 30, "xx:22"),
            [199_156] = new(199_156, "NpcHandler.EventOutxx22", 10_760, 26, 30, "xx:26"),
            [199_157] = new(199_157, "NpcHandler.EventOutx26", 10_593, 31, 45, "xx:26"),
            [199_158] = new(199_158, "NpcHandler.EventOutxx31", 10_763, 35, 59, "xx:35"),
            [199_159] = new(199_159, "NpcHandler.EventOutxx35", 10_299, 38, 59, "xx:38"),
            [199_160] = new(199_160, "NpcHandler.EventOutxx41", 10_079, 41, 59, "xx:41"),
            [199_161] = new(199_161, "NpcHandler.EventOutx44", 3_872, 44, 59, "xx:44"),
            [199_162] = new(199_162, "NpcHandler.EventOutxx48", 10_759, 48, 59, "xx:48"),
            [199_163] = new(199_163, "NpcHandler.EventOutxx56", 10_301, 56, 59, "xx:56"),
        };

    internal static ValueTask<bool> TryHandleLegacyHourlyEventsNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return TryHandleLegacyHourlyEventsNpcAsync(
            context,
            session,
            npcId,
            optionId,
            DateTime.Now,
            cancellationToken);
    }

    internal static async ValueTask<bool> TryHandleLegacyHourlyEventsNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacyEventsInHourNpcId)
        {
            return await TryHandleLegacyEventsInHourAsync(context, session, optionId, now, cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyHourlyEventExits.TryGetValue(npcId, out var exit))
        {
            return await TryHandleLegacyHourlyEventExitAsync(context, session, exit, optionId, now, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyEventsInHourAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.EventsInHour";

        if (optionId == 0)
        {
            var options = new List<LegacyNpcDialogOption>
            {
                new("Exchange tokens [Saturn]", 55),
                new("Saturn [Available]", 40),
            };
            foreach (var menuEntry in LegacyHourlyEventEntries.Values.OrderBy(static entry => entry.OptionId))
            {
                if (menuEntry.IsOpen(now.Minute))
                {
                    options.Add(new LegacyNpcDialogOption(menuEntry.MenuText, menuEntry.OptionId));
                }
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Hi. Welcome to [CoFreedom3D] events. Enjoy with us."],
                    options,
                    LegacyHourlyEventsMenuAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await LogLegacyHourlyEventsGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Arena qualifier status and Saturn scheduler Mode are not locally represented; Saturn availability is source-shaped.")
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 40)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "Hey, nice choice! I can explain Saturn's map to you.",
                        "In Saturn, you can enter a planet-like map. You can collect tokens by opening golden chests.",
                        "Each golden chest gives you 1 Arrest Token, which you can exchange for cool rewards.",
                        "Saturn's planet can contain up to 3 golden chests at the same time.",
                        "They respawn every 10 seconds. Note: if you are killed, your tokens will drop!",
                    ],
                    LegacyHourlyEventsSaturnInfoOptions,
                    LegacyHourlyEventsSaturnAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 45)
        {
            await LogLegacyHourlyEventsGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "MsgSchedules.Saturn.Mode and random map-2353 coordinates are not locally represented.")
                .ConfigureAwait(false);
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Hey, Saturn's planet is too hot right now. Come back another time to cool down."],
                    [new LegacyNpcDialogOption("My bad!", LegacyNpcClosedOption)],
                    LegacyHourlyEventsSaturnAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 55)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "Hey, you did great. Want your prize? You can exchange Arrest Tokens for rich rewards.",
                        $"Your current Arrest Token count: {CountLegacyHourlyEventTokens(session).ToString(CultureInfo.InvariantCulture)}.",
                    ],
                    LegacyHourlyEventsSaturnTokenOptions,
                    LegacyHourlyEventsUnavailableAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 107)
        {
            return await TryExchangeLegacyHourlyEventArrestTokenAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (optionId == 109)
        {
            return await TryExchangeLegacyHourlyEventClassExperienceAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (optionId == 106)
        {
            return await TryExchangeLegacyHourlyEventRandomRelicAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (optionId is 111 or 112 or 113)
        {
            // The decompiled outer cases 111-113 re-switch against 11-13, leave item id zero, and exit.
            await context.Log.WriteLineAsync(
                    "WorldServer preserved LegacyNpc HourlyEvents Saturn rune-fragment source no-op " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    "outerOptions=111,112,113 innerOptions=11,12,13; no inventory or fragment mutation occurred.")
                .ConfigureAwait(false);
            return true;
        }

        if (!LegacyHourlyEventEntries.TryGetValue(optionId, out var selectedEntry))
        {
            return false;
        }

        if (selectedEntry.OptionId == 2)
        {
            await LogLegacyHourlyEventsGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "MsgSchedules.CurrentTournament Type/Join state is not locally represented.")
                .ConfigureAwait(false);
            await SendLegacyHourlyEventUnavailableAsync(context, sourceMethod, optionId, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!selectedEntry.IsOpen(now.Minute))
        {
            if (optionId == 1)
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket
                            .SystemTalk("The Tomb event has finished. Please come back after ##:5.")
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer sent LegacyNpc HourlyEvents closed system message " +
                        $"{FormatSession(context)} source={sourceMethod} option={optionId}.")
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyHourlyEventUnavailableAsync(context, sourceMethod, optionId, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var entrySession = ReviveLegacyHourlyEventSessionIfNeeded(session);
        if (!ReferenceEquals(entrySession, session))
        {
            await LogLegacyHourlyEventsGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "Source calls Player.Revive before teleport; local revive is limited to restoring MinHitPoints for this entry.")
                .ConfigureAwait(false);
        }

        await SendLegacyNpcTeleportAsync(
                context,
                entrySession,
                LegacyEventsInHourNpcId,
                optionId,
                new LegacyNpcTeleportDestination(selectedEntry.MapId, selectedEntry.X, selectedEntry.Y, sourceMethod),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryExchangeLegacyHourlyEventArrestTokenAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryConsumeLegacyInventoryItemCount(
                session.PlayerItems,
                LegacyHourlyEventsArrestTokenItemId,
                LegacyHourlyEventsArrestTokenExchangeCount,
                out var updatedItems,
                out var itemFrames))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["You don't have enough Arrest Tokens!"],
                    [new LegacyNpcDialogOption("I understand.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc HourlyEvents Saturn token exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"itemId={LegacyHourlyEventsArrestTokenItemId} required={LegacyHourlyEventsArrestTokenExchangeCount}; " +
                    "source inventory token is missing.")
                .ConfigureAwait(false);
            return true;
        }

        var previousConquerPoints = session.Character.ConquerPoints;
        var updatedConquerPoints = checked(previousConquerPoints + LegacyHourlyEventsArrestTokenConquerPoints);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyHourlyEventSaturnExchangeAsync(updatedContext, updatedSession, sourceMethod)
            .ConfigureAwait(false);
        foreach (var itemFrame in itemFrames)
        {
            await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                    CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                    [new LegacyMsgUpdateEntry(
                        LegacyMsgUpdateDataType.ConquerPoints,
                        checked((ulong)updatedConquerPoints))])
                .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                ["Congratulations! You received 200,000 Conquer Points."],
                [new LegacyNpcDialogOption("Thank you.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc HourlyEvents Saturn token exchange " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"itemId={LegacyHourlyEventsArrestTokenItemId} consumed={LegacyHourlyEventsArrestTokenExchangeCount} " +
                $"previousConquerPoints={previousConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"updatedConquerPoints={updatedConquerPoints.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryExchangeLegacyHourlyEventRandomRelicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryConsumeLegacyHourlyEventRandomRelicTokens(
                session.PlayerItems,
                out var consumedItems,
                out var consumeFrames))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["You don't have enough Arrest Tokens!"],
                    [new LegacyNpcDialogOption("I understand.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc HourlyEvents Saturn random relic exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"itemId={LegacyHourlyEventsArrestTokenItemId} " +
                    $"required={LegacyHourlyEventsRandomRelicTokenExchangeCount}; " +
                    "neither the unbound nor bound source inventory class contains three tokens.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.Sessions is not { } sessions ||
            context.Session is null ||
            !ReferenceEquals(context.Session, session) ||
            !sessions.TryGet(context.Connection, out var activeSession) ||
            activeSession is null ||
            !ReferenceEquals(activeSession, session))
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "sessionUnavailable")
                .ConfigureAwait(false);
            return true;
        }

        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "playerItemsRootMissing")
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.Uid <= 0)
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "characterUidUnavailable")
                .ConfigureAwait(false);
            return true;
        }

        if (context.WorldData is not { } worldData)
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "randomRelicWorldDataMissing")
                .ConfigureAwait(false);
            return true;
        }

        var relicCatalog = LegacyBossSpecialDeathDropCatalog.FromWorldData(worldData);
        if (!LegacyBossSpecialDeathDropPolicy.TryCreateDeityLandRandomRelicItem(
                relicCatalog,
                CreateLegacyBossSpecialDeathDropRolls(session.Character),
                out var generated,
                out var missingReason))
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"randomRelicDataUnavailable:{missingReason}")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryReadItemType(worldData, generated.ItemId, out var rewardItemType) ||
            string.IsNullOrWhiteSpace(rewardItemType.Name))
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"randomRelicItemTypeUnavailable:{generated.ItemId}")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(consumedItems, 1))
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "inventoryFullAfterSourceRemoval")
                .ConfigureAwait(false);
            return true;
        }

        PlayerItemSnapshotItem rewardItem;
        try
        {
            rewardItem = CreateLegacyGeneratedBossSpecialDropItem(
                generated,
                GetNextSessionItemUid(consumedItems));
        }
        catch (OverflowException)
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "itemUidUnavailable")
                .ConfigureAwait(false);
            return true;
        }

        var updatedItems = consumedItems with
        {
            InventoryItems = consumedItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        if (!TryPersistLegacyHourlyEventRandomRelicItems(
                playerItemsRoot,
                session.Character.Uid,
                updatedItems,
                out var itemPath,
                out var persistenceReason))
        {
            await LogLegacyHourlyEventRandomRelicFenceAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"itemPersistenceUnavailable:{persistenceReason}")
                .ConfigureAwait(false);
            return true;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc HourlyEvents Saturn random relic exchange items " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(itemPath)}.")
            .ConfigureAwait(false);
        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var consumeFrame in consumeFrames)
        {
            await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, worldData.ItemsById),
                        LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"Congratulations! You received 1 {rewardItemType.Name}."],
                [new LegacyNpcDialogOption("Thank you.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc HourlyEvents Saturn random relic exchange " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"itemId={LegacyHourlyEventsArrestTokenItemId} " +
                $"consumed={LegacyHourlyEventsRandomRelicTokenExchangeCount} " +
                $"rewardItemId={rewardItem.ItemId} rewardItemUid={rewardItem.Uid} " +
                $"rewardName=\"{rewardItemType.Name}\" bound={rewardItem.Bound} " +
                $"durability={rewardItem.Durability}/{rewardItem.MaximumDurability} " +
                $"path={Path.GetFileName(itemPath)} generatedSource=\"{generated.Source}\".")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryConsumeLegacyHourlyEventRandomRelicTokens(
        PlayerItemSnapshot items,
        out PlayerItemSnapshot updatedItems,
        out IReadOnlyList<byte[]> frames)
    {
        updatedItems = items;
        frames = [];

        ulong unboundCount = 0;
        ulong boundCount = 0;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != LegacyHourlyEventsArrestTokenItemId)
            {
                continue;
            }

            if (item.Bound == 0)
            {
                unboundCount = checked(unboundCount + item.StackSize);
            }
            else
            {
                boundCount = checked(boundCount + item.StackSize);
            }
        }

        // Inventory.Remove first qualifies one bound class, then removes matching items without a bound filter.
        if (unboundCount < LegacyHourlyEventsRandomRelicTokenExchangeCount &&
            boundCount < LegacyHourlyEventsRandomRelicTokenExchangeCount)
        {
            return false;
        }

        var inventoryItems = items.InventoryItems.ToList();
        var mutationFrames = new List<byte[]>(LegacyHourlyEventsRandomRelicTokenExchangeCount);
        var consumed = 0;
        for (var pass = 0;
             pass < LegacyHourlyEventsRandomRelicTokenExchangeCount &&
             consumed < LegacyHourlyEventsRandomRelicTokenExchangeCount;
             pass++)
        {
            var candidateUids = inventoryItems
                .Where(item => item.ItemId == LegacyHourlyEventsArrestTokenItemId)
                .Select(item => item.Uid)
                .ToArray();
            foreach (var candidateUid in candidateUids)
            {
                var index = inventoryItems.FindIndex(item => item.Uid == candidateUid);
                if (index < 0)
                {
                    continue;
                }

                var item = inventoryItems[index];
                if (item.StackSize > 1)
                {
                    var updatedItem = item.WithStackSize(checked((ushort)(item.StackSize - 1)));
                    inventoryItems[index] = updatedItem;
                    mutationFrames.Add(
                        LegacyGameItemPacket
                            .FromSnapshotItem(updatedItem, mode: LegacyGameItemPacket.UpdateItemMode)
                            .ToFrameBytes());
                }
                else
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
                }

                consumed++;
                if (consumed == LegacyHourlyEventsRandomRelicTokenExchangeCount)
                {
                    break;
                }
            }
        }

        if (consumed != LegacyHourlyEventsRandomRelicTokenExchangeCount)
        {
            return false;
        }

        updatedItems = items with { InventoryItems = inventoryItems.ToArray() };
        frames = mutationFrames;
        return true;
    }

    private static bool TryPersistLegacyHourlyEventRandomRelicItems(
        string playerItemsRoot,
        long characterUid,
        PlayerItemSnapshot items,
        out string itemPath,
        out string reason)
    {
        itemPath = "";
        reason = "";
        string? temporaryPath = null;
        try
        {
            var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(items);
            Directory.CreateDirectory(playerItemsRoot);
            itemPath = Path.Combine(
                playerItemsRoot,
                characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
            temporaryPath = itemPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporaryPath, bytes);
            File.Move(temporaryPath, itemPath, overwrite: true);
            temporaryPath = null;
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException or
            System.Security.SecurityException)
        {
            itemPath = "";
            reason = ex.GetType().Name;
            return false;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex) when (
                    ex is IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    System.Security.SecurityException)
                {
                    // The original snapshot remains authoritative when temporary cleanup is unavailable.
                }
            }
        }
    }

    private static async ValueTask LogLegacyHourlyEventRandomRelicFenceAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc HourlyEvents Saturn random relic exchange " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId} reason={reason}; " +
                "no token, reward, session, persistence, or packet mutation was committed.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryExchangeLegacyHourlyEventClassExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryConsumeLegacyHourlyEventArrestTokensUnitWise(
                session.PlayerItems,
                LegacyHourlyEventsClassExperienceTokenExchangeCount,
                out var updatedItems,
                out var itemFrames))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["You don't have enough Arrest Tokens!"],
                    [new LegacyNpcDialogOption("I understand.", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc HourlyEvents Saturn class experience exchange " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId} " +
                    $"itemId={LegacyHourlyEventsArrestTokenItemId} " +
                    $"required={LegacyHourlyEventsClassExperienceTokenExchangeCount}; " +
                    "source inventory tokens are insufficient.")
                .ConfigureAwait(false);
            return true;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyHourlyEventClassExperiencePersistenceGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "sessionStoreMissing")
                .ConfigureAwait(false);
            return true;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await LogLegacyHourlyEventClassExperiencePersistenceGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "playerItemsRootMissing")
                .ConfigureAwait(false);
            return true;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await LogLegacyHourlyEventClassExperiencePersistenceGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    "usersRootMissing")
                .ConfigureAwait(false);
            return true;
        }

        var previousClassExperience = ReadAttributeUInt64(session.Character.Attributes, "ClassExperience");
        var updatedClassExperience = checked(
            previousClassExperience + LegacyHourlyEventsClassExperienceReward);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            "ClassExperience",
            updatedClassExperience);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };

        var itemLoader = new PlayerItemSnapshotLoader(context.PlayerItemsRoot);
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
            await LogLegacyHourlyEventClassExperiencePersistenceGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"itemSnapshotSaveFailed:{ex.GetType().Name}")
                .ConfigureAwait(false);
            return true;
        }

        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                updatedCharacter.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ClassExperience"] = updatedClassExperience.ToString(CultureInfo.InvariantCulture),
                },
                out var characterPath,
                out var characterReason))
        {
            var itemRollbackReason = "none";
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
                itemRollbackReason = ex.GetType().Name;
            }

            await LogLegacyHourlyEventClassExperiencePersistenceGapAsync(
                    context,
                    sourceMethod,
                    optionId,
                    $"characterPersistenceFailed:{characterReason};itemRollback={itemRollbackReason}")
                .ConfigureAwait(false);
            return true;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc HourlyEvents Saturn class experience exchange character " +
                $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(characterPath)} " +
                "keys=ClassExperience.")
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyNpc HourlyEvents Saturn class experience exchange items " +
                $"{FormatSession(context)} source={sourceMethod} path={updatedCharacter.Uid}.bin.")
            .ConfigureAwait(false);

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var itemFrame in itemFrames)
        {
            await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                    CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                    [new LegacyMsgUpdateEntry(
                        LegacyMsgUpdateDataType.ClassExperience,
                        updatedClassExperience)])
                .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                sourceMethod,
                [$"Congratulations! You received {LegacyHourlyEventsClassExperienceDialogReward.ToString(CultureInfo.InvariantCulture)} Divine Class Experience."],
                [new LegacyNpcDialogOption("Thank you.", LegacyNpcClosedOption)],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc HourlyEvents Saturn class experience exchange " +
                $"{FormatSession(updatedContext)} source={sourceMethod} option={optionId} " +
                $"itemId={LegacyHourlyEventsArrestTokenItemId} " +
                $"consumed={LegacyHourlyEventsClassExperienceTokenExchangeCount} " +
                $"previousClassExperience={previousClassExperience.ToString(CultureInfo.InvariantCulture)} " +
                $"updatedClassExperience={updatedClassExperience.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceDialogClassExperience={LegacyHourlyEventsClassExperienceDialogReward.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryConsumeLegacyHourlyEventArrestTokensUnitWise(
        PlayerItemSnapshot items,
        ushort count,
        out PlayerItemSnapshot updatedItems,
        out IReadOnlyList<byte[]> frames)
    {
        updatedItems = items;
        frames = [];

        ulong unboundCount = 0;
        ulong boundCount = 0;
        foreach (var item in items.InventoryItems)
        {
            if (item.ItemId != LegacyHourlyEventsArrestTokenItemId)
            {
                continue;
            }

            var stackSize = Math.Max(1, (int)item.StackSize);
            if (item.Bound == 0)
            {
                unboundCount = checked(unboundCount + (uint)stackSize);
            }
            else
            {
                boundCount = checked(boundCount + (uint)stackSize);
            }
        }

        // Inventory.Remove qualifies one bound class, then removes matching items without a bound filter.
        if (unboundCount < count && boundCount < count)
        {
            return false;
        }

        var inventoryItems = items.InventoryItems.ToList();
        var mutationFrames = new List<byte[]>(count);
        var consumed = 0;
        for (var pass = 0; pass < count && consumed < count; pass++)
        {
            var candidateUids = inventoryItems
                .Where(item => item.ItemId == LegacyHourlyEventsArrestTokenItemId)
                .Select(item => item.Uid)
                .ToArray();
            foreach (var candidateUid in candidateUids)
            {
                var index = inventoryItems.FindIndex(item => item.Uid == candidateUid);
                if (index < 0)
                {
                    continue;
                }

                var item = inventoryItems[index];
                var stackSize = Math.Max(1, (int)item.StackSize);
                if (stackSize > 1)
                {
                    var updatedItem = item.WithStackSize(checked((ushort)(stackSize - 1)));
                    inventoryItems[index] = updatedItem;
                    mutationFrames.Add(
                        LegacyGameItemPacket
                            .FromSnapshotItem(updatedItem, mode: LegacyGameItemPacket.UpdateItemMode)
                            .ToFrameBytes());
                }
                else
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
                }

                consumed++;
                if (consumed == count)
                {
                    break;
                }
            }
        }

        if (consumed != count)
        {
            return false;
        }

        updatedItems = items with { InventoryItems = inventoryItems.ToArray() };
        frames = mutationFrames;
        return true;
    }

    private static async ValueTask LogLegacyHourlyEventClassExperiencePersistenceGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc HourlyEvents Saturn class experience exchange persistence " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId} reason={reason}; " +
                "no session mutation or exchange packets were committed.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyHourlyEventSaturnExchangeAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        string sourceMethod)
    {
        if (TryPersistLegacyCharacterAttributes(
                updatedSession.UsersRoot,
                updatedSession.AccountId,
                updatedSession.Character.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = updatedSession.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                out var characterPath,
                out var characterReason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc HourlyEvents Saturn exchange character " +
                    $"{FormatSession(context)} source={sourceMethod} path={Path.GetFileName(characterPath)} keys=ConquerPoints.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc HourlyEvents Saturn exchange character persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason={characterReason}.")
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc HourlyEvents Saturn exchange item persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason=playerItemsRootMissing.")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedSession.PlayerItems);
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc HourlyEvents Saturn exchange items " +
                    $"{FormatSession(context)} source={sourceMethod} path={updatedSession.Character.Uid}.bin.")
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc HourlyEvents Saturn exchange item persistence " +
                    $"{FormatSession(context)} source={sourceMethod} reason=itemSnapshotSaveFailed " +
                    $"exception={ex.GetType().Name}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask<bool> TryHandleLegacyHourlyEventExitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyHourlyEventExit exit,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        exit.SourceMethod,
                        ["Hello, are you the last player alive?"],
                        LegacyHourlyEventsExitOptions,
                        LegacyHourlyEventsExitAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (!exit.IsRewardOpen(now.Minute))
                {
                    await SendLegacyHourlyEventEndDialogAsync(context, exit, cancellationToken).ConfigureAwait(false);
                    return true;
                }

                return await TryClaimLegacyHourlyEventPrizeAsync(context, session, exit, optionId, cancellationToken)
                    .ConfigureAwait(false);

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        exit.SourceMethod,
                        ["Do you want to teleport back to Twin City? Are you sure?"],
                        LegacyHourlyEventsExitConfirmOptions,
                        LegacyHourlyEventsExitAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        exit.NpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, exit.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryClaimLegacyHourlyEventPrizeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyHourlyEventExit exit,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var alivePlayers = CountLegacyHourlyEventAlivePlayers(context, session, exit.EventMapId);
        if (alivePlayers != 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc HourlyEvents prize " +
                    $"{FormatSession(context)} source={exit.SourceMethod} option={optionId} " +
                    $"map={exit.EventMapId} alivePlayers={alivePlayers}; source requires exactly one alive player.")
                .ConfigureAwait(false);
            return true;
        }

        var previousConquerPoints = session.Character.ConquerPoints;
        var updatedConquerPoints = checked(previousConquerPoints + TunedLegacyHourlyEventPrizeConquerPoints);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                    CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)updatedConquerPoints))])
                .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await BroadcastLegacyHourlyEventPrizeAsync(context with { Session = updatedSession }, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        await PersistLegacyHourlyEventPrizeAsync(context, updatedSession, exit.SourceMethod).ConfigureAwait(false);
        await SendLegacyNpcTeleportAsync(
                context with { Session = updatedSession },
                updatedSession,
                exit.NpcId,
                optionId,
                new LegacyNpcTeleportDestination(1_002, 410, 355, exit.SourceMethod),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc HourlyEvents prize " +
                $"{FormatSession(context with { Session = updatedSession })} source={exit.SourceMethod} " +
                $"option={optionId} map={exit.EventMapId} alivePlayers={alivePlayers} " +
                $"sourceConquerPoints={LegacyHourlyEventSourcePrizeConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"tunedConquerPoints={TunedLegacyHourlyEventPrizeConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"previousConquerPoints={previousConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"updatedConquerPoints={updatedConquerPoints.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask BroadcastLegacyHourlyEventPrizeAsync(
        WorldPacketHandlerContext context,
        WorldSession winner,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemCenter(
                $" Congratulations {winner.Character.Name} claimed the prize [ {TunedLegacyHourlyEventPrizeConquerPoints.ToString(CultureInfo.InvariantCulture)} Cps ] for winning the Top TopHours +   .")
            .ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        var sent = 0;
        if (activeSessions is { Count: > 0 })
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                sent++;
            }
        }
        else
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            sent = 1;
        }

        await context.Log.WriteLineAsync(
                "WorldServer broadcast LegacyNpc HourlyEvents prize " +
                $"{FormatSession(context)} winner={winner.Character.Name} count={sent}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyHourlyEventPrizeAsync(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        string sourceMethod)
    {
        if (TryPersistLegacyCharacterAttributes(
                updatedSession.UsersRoot,
                updatedSession.AccountId,
                updatedSession.Character.Uid,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = updatedSession.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc HourlyEvents prize " +
                    $"{FormatSession(context with { Session = updatedSession })} source={sourceMethod} " +
                    $"path={Path.GetFileName(path)} keys=ConquerPoints.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc HourlyEvents prize persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source={sourceMethod} reason={reason}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyHourlyEventUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["Sorry, you cannot enter right now. Please wait until the match starts!"],
                LegacyHourlyEventsUnavailableOptions,
                LegacyHourlyEventsUnavailableAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc HourlyEvents unavailable dialog " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyHourlyEventEndDialogAsync(
        WorldPacketHandlerContext context,
        LegacyHourlyEventExit exit,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                exit.SourceMethod,
                [$"Top ends at {exit.EndMinuteText}."],
                LegacyHourlyEventsEndOptions,
                LegacyHourlyEventsEndAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static int CountLegacyHourlyEventAlivePlayers(
        WorldPacketHandlerContext context,
        WorldSession currentSession,
        long mapId)
    {
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is not { Count: > 0 })
        {
            return IsLegacyHourlyEventAliveOnMap(currentSession, mapId) ? 1 : 0;
        }

        return activeSessions.Count(active => IsLegacyHourlyEventAliveOnMap(active.Session, mapId));
    }

    private static bool IsLegacyHourlyEventAliveOnMap(WorldSession session, long mapId)
    {
        return session.Character.MapId == mapId && session.Character.MinHitPoints >= 1;
    }

    private static WorldSession ReviveLegacyHourlyEventSessionIfNeeded(WorldSession session)
    {
        return session.Character.MinHitPoints >= 1
            ? session
            : session with { Character = session.Character with { MinHitPoints = 1 } };
    }

    private static int CountLegacyHourlyEventTokens(WorldSession session)
    {
        return session.PlayerItems.InventoryItems
            .Where(static item => item.ItemId == LegacyHourlyEventsArrestTokenItemId)
            .Sum(static item => Math.Max(1, (int)item.StackSize));
    }

    private static async ValueTask LogLegacyHourlyEventsGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc HourlyEvents " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
    }

    private readonly record struct LegacyHourlyEventEntry(
        byte OptionId,
        int StartMinute,
        int EndMinute,
        string MenuText,
        long MapId,
        long X,
        long Y)
    {
        public bool IsOpen(int minute)
        {
            return minute >= StartMinute && minute <= EndMinute;
        }
    }

    private readonly record struct LegacyHourlyEventExit(
        uint NpcId,
        string SourceMethod,
        long EventMapId,
        int RewardStartMinute,
        int RewardEndMinute,
        string EndMinuteText)
    {
        public bool IsRewardOpen(int minute)
        {
            return minute >= RewardStartMinute && minute <= RewardEndMinute;
        }
    }
}
