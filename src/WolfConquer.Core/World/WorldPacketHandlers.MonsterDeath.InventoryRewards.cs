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
    private static async ValueTask<WorldSession> TryApplyLegacyDeityLandQuest3944MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 10250 quest 3944";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        if (ShouldAutoAcceptLegacyDeityLandQuest3944(updatedSession.Character.PlayerQuests))
        {
            var intentionCount = TryGetLegacyQuestMission(context.WorldData, LegacyDeityLandQuestId, out var mission)
                ? Math.Max(1, GetLegacyQuestIntentionCount(mission))
                : 1;
            var acceptedQuest = new PlayerQuestSnapshotEntry(
                LegacyDeityLandQuestId,
                PlayerQuestStatus.Accepted,
                Time: 0,
                Intentions: new uint[intentionCount]);

            updatedSession = WithPlayerQuests(
                updatedSession,
                updatedSession.Character.PlayerQuests.WithQuest(acceptedQuest));
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = updatedContext with { Session = updatedSession };

            await context.Connection.SendAsync(
                    LegacyQuestListPacket.CreateSingleQuestFrame(
                        LegacyQuestListMode.AcceptQuest,
                        ToLegacyQuestListItem(acceptedQuest)),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer accepted legacy DeityLand monster death quest " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={LegacyDeityLandQuestId} intentions={intentionCount} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (!updatedSession.Character.PlayerQuests.TryGetQuest(LegacyDeityLandQuestId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy DeityLand monster death quest increment " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={LegacyDeityLandQuestId} status={(quest is null ? "missing" : quest.Status)} " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                updatedContext,
                updatedSession,
                LegacyDeityLandQuestId,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(LegacyDeityLandQuestId, out var updatedQuest) &&
            updatedQuest.Intentions.Count != 0 &&
            updatedQuest.Intentions[0] >= 100)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(
                            "You have killed 100 devils in the DeityLand! Go claim your rewards from the Victoy Chest!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy DeityLand monster death quest completion prompt " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={LegacyDeityLandQuestId} objective0={updatedQuest.Intentions[0]} " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static bool ShouldAutoAcceptLegacyDeityLandQuest3944(PlayerQuestSnapshot playerQuests)
    {
        return !playerQuests.TryGetQuest(LegacyDeityLandQuestId, out var quest) ||
            quest.Status == PlayerQuestStatus.Available;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyDeityLandInventoryRewardsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 10_250)
        {
            return session;
        }

        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 10250 Inventory.HaveSpace(1)";
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Your inventory is too full to contain more chest keys.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy DeityLand inventory rewards " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId}; " +
                    $"inventory is full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        if (CheckLegacyDeityLandInventoryRewardRate(
                updatedSession.Character,
                RuntimeLegacyDeityLandReward4050001RateAttribute,
                ratePercent: 4))
        {
            updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                    updatedContext,
                    updatedSession,
                    itemId: 4_050_001,
                    sourceMessage: "You have got 1 Rune!",
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(4) AddItemWitchStack(4050001)",
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        if (CheckLegacyDeityLandInventoryRewardRate(
                updatedSession.Character,
                RuntimeLegacyDeityLandReward4060001RateAttribute,
                ratePercent: 5))
        {
            updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardAsync(
                    updatedContext,
                    updatedSession,
                    itemId: 4_060_001,
                    sourceMessage: "You have got 1 Rune!",
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(5) AddItemWitchStack(4060001)",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (CheckLegacyDeityLandInventoryRewardRate(
                updatedSession.Character,
                RuntimeLegacyDeityLandRedRuneFragmentRateAttribute,
                ratePercent: 1))
        {
            await SendLegacyDeityLandInventoryRewardNoticeAsync(
                    updatedContext,
                    updatedSession,
                    "You have got 1 RedRuneFragment!",
                    "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(1) RedRuneFragment notice",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (CheckLegacyDeityLandInventoryRewardRate(
                updatedSession.Character,
                RuntimeLegacyDeityLandYellowRuneFragmentRateAttribute,
                ratePercent: 1))
        {
            await SendLegacyDeityLandInventoryRewardNoticeAsync(
                    updatedContext,
                    updatedSession,
                    "You have got 1 YellowRuneFragment!",
                    "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(1) YellowRuneFragment notice",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
                updatedContext,
                updatedSession,
                itemId: 3_306_368,
                ratePercent: 0.007,
                sourceMessage: "You have got 1 BlueRuneFragment!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(0.007) AddItemWitchStack(3306368)",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
                updatedContext,
                updatedSession,
                itemId: 3_330_770,
                ratePercent: 0.007,
                sourceMessage: "You have got 1 Ex!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(0.007) AddItemWitchStack(3330770)",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
                updatedContext,
                updatedSession,
                itemId: 3_316_793,
                ratePercent: 0.007,
                sourceMessage: "You have got 1 Ex!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(0.007) AddItemWitchStack(3316793)",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
                updatedContext,
                updatedSession,
                itemId: 3_009_102,
                ratePercent: 3,
                sourceMessage: "You have got 1 MoonFruit!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(3) AddItemWitchStack(3009102)",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
                updatedContext,
                updatedSession,
                itemId: 3_009_103,
                ratePercent: 2,
                sourceMessage: "You have got 1 SoulFruit!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(2) AddItemWitchStack(3009103)",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
                updatedContext,
                updatedSession,
                itemId: 3_009_104,
                ratePercent: 0.001,
                sourceMessage: "You have got 1 RedBodhiBean!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(0.001) AddItemWitchStack(3009104)",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryGrantLegacyDeityLandStackedInventoryRewardIfRateHitAsync(
                updatedContext,
                updatedSession,
                itemId: 4_040_001,
                ratePercent: 22,
                sourceMessage: "You have got 1 Rune!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 10250 Core.Rate(22) AddItemWitchStack(4040001)",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryGrantLegacyDeityLandChainedInventoryRewardAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (CheckLegacyDeityLandInventoryRewardRate(
                updatedSession.Character,
                RuntimeLegacyDeityLandInventoryRelicFirstRateAttribute,
                ratePercent: 1) &&
            CheckLegacyDeityLandInventoryRewardRate(
                updatedSession.Character,
                RuntimeLegacyDeityLandInventoryRelicSecondRateAttribute,
                ratePercent: 1))
        {
            updatedSession = await TryGrantLegacyDeityLandRandomRelicInventoryRewardAsync(
                    updatedContext,
                    updatedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
        }

        updatedSession = await TryGrantLegacyDeityLandChestKeyInventoryRewardAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyDeityLandWarDevilRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 10_250 ||
            !monster.Name.Contains("WarDevil", StringComparison.Ordinal))
        {
            return session;
        }

        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 10250 && Name.Contains(\"WarDevil\")";
        var updatedSession = session;
        uint itemId;
        string rollBranch;
        byte? sourceCount = null;
        var luckyPoints = ReadAttributeUInt64(session.Character.Attributes, RuntimeLegacyDeityLandLuckyPointsAttribute);
        if (luckyPoints >= 10)
        {
            var remainingLuckyPoints = checked(luckyPoints - 10);
            var attributes = new Dictionary<string, string>(
                session.Character.Attributes,
                StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyDeityLandLuckyPointsAttribute] =
                    remainingLuckyPoints.ToString(CultureInfo.InvariantCulture),
            };
            updatedSession = session with
            {
                Character = session.Character with { Attributes = attributes },
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk(
                            "You've consumed 10 Luck Points, and still have " +
                            $"{remainingLuckyPoints.ToString(CultureInfo.InvariantCulture)} Luck Points.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            var luckyRoll = SelectLegacyDeityLandWarDevilLuckyRewardRoll(session.Character);
            itemId = LegacyDeityLandWarDevilLuckyRewardItems[luckyRoll - 1];
            rollBranch =
                FormattableString.Invariant(
                    $"DeityLandLuckyPoints >= 10 Pool.GetRandom.Next(1, 24) roll={luckyRoll}");
        }
        else
        {
            var baseRoll = SelectLegacyDeityLandWarDevilBaseRewardRoll(session.Character);
            var selected = LegacyDeityLandWarDevilBaseRewardItems[baseRoll - 1];
            itemId = selected.ItemId;
            sourceCount = selected.SourceCount;
            rollBranch =
                FormattableString.Invariant(
                    $"DeityLandLuckyPoints < 10 Pool.GetRandom.Next(1, 6) roll={baseRoll} sourceCount={selected.SourceCount}");
        }

        var itemName = ResolveLegacyItemName(context.WorldData, itemId);
        var message =
            $"{session.Character.Name} defeated a devil in the Deityland and received a rare treasure {itemName}!";
        var messageFrame = LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        var sent = 0;
        if (activeSessions is { Count: > 0 })
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
                sent++;
            }
        }
        else
        {
            await context.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
            sent = 1;
        }

        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "angelwing")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy DeityLand WarDevil reward notice " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"itemId={itemId} sourceCount={(sourceCount is { } count ? count.ToString(CultureInfo.InvariantCulture) : "n/a")} " +
                $"broadcastCount={sent} branch=\"{sourceBranch}; {rollBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask TryApplyLegacyDeityLandSpriteRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 10_250 ||
            !monster.Name.Contains("Sprite", StringComparison.Ordinal))
        {
            return;
        }

        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 10250 && Name.Contains(\"Sprite\")";
        var itemId = monster.Name switch
        {
            "MeteorSprite" => 1_088_001U,
            "DBSprite" => 3_303_975U,
            "ChiSprite" => 3_008_305U,
            "StoneSprite" => SelectLegacyDeityLandStoneSpriteRewardItemId(session.Character),
            "DragonSoulSprite" => 3_005_133U,
            _ => 0U,
        };

        var sentCount = 0;
        var dropKind = "none";
        uint moneyAmount = 0;
        if (string.Equals(monster.Name, "SilverSprite", StringComparison.Ordinal))
        {
            moneyAmount = 50_000U;
            itemId = GetLegacyMoneyItemId(moneyAmount);
            dropKind = "money";
            sentCount = await SendLegacyMapSpecialMonsterDeathMoneyDropAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    itemId,
                    moneyAmount,
                    scatterRange: 0,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (itemId != 0)
        {
            dropKind = "item";
            sentCount = await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    itemId,
                    count: 1,
                    scatterRange: 3,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "angelwing")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy DeityLand Sprite reward " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                $"dropKind={dropKind} itemId={itemId} moneyAmount={moneyAmount} sent={sentCount} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacySaturnMonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        var saturnAlive = TryReadUInt32Attribute(
                session.Character.Attributes,
                out var configuredSaturnAlive,
                RuntimeLegacySaturnAliveAttribute)
            ? configuredSaturnAlive != 0
            : true;

        if (!saturnAlive)
        {
            return await SendLegacyMonsterDeathTeleportAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    mapId: 1_002,
                    x: 410,
                    y: 354,
                    sourceBranch: "MonsterRole.cs killer.Player.Map == 2353 && MsgSchedules.Saturn.Mode != Alive",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 2353 && MsgSchedules.Saturn.Mode == Alive";
        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Your inventory is full.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy Saturn monster death Arrest Token " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId}; " +
                    $"inventory is full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, LegacyArrestTokenItemId, out var arrestTokenType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "saturn-arrest-token-item",
                        reason: "source grants item 711624 on map 2353 monster death while MsgSchedules.Saturn is alive, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", LegacyArrestTokenItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", LegacyArrestTokenItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var arrestToken = CreatePurchasedItems(session.PlayerItems, arrestTokenType, count: 1)[0];
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([arrestToken]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(arrestToken).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You have got 1 Arrest Token!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy Saturn monster death Arrest Token " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"map={monster.MapId} itemId={LegacyArrestTokenItemId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
