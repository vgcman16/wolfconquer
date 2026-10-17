using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1011Family106RockMonsterQuest1753SmellAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_753;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1011 && Family.ID == 106 quest 1753 Rock Monster smell";

        if (monster.MapId != 1_011 ||
            monsterFamily.Id != 106 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                context,
                session,
                questId,
                [1, 0, 0],
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You,ve~collected~some~smells.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        var completed = updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            IsLegacyQuestObjectiveSlotReached(updatedQuest.Intentions, 0, 10);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("You,ve collected the smell of Rock Monster!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1011 family 106 Rock Monster smell objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1011Family7BanditQuest1114GrainAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_114;
        const uint itemId = 711_322;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1011 && Family.ID == 7 quest 1114 Bandit grain";

        if (monster.MapId != 1_011 ||
            monsterFamily.Id != 7 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context,
                session,
                questId,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1011-family7-bandit-grain",
                        reason: "source AddItemWitchStack(711322) grants a bag of grain for quest 1114, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
            return updatedSession;
        }

        var updatedItems = AddOrStackLegacyInventoryItem(
            updatedSession.PlayerItems,
            itemType,
            itemId,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        updatedSession = updatedSession with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You've killed the Bandit and received a bag of grain!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var completed = updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            IsLegacyQuestObjectiveReached(updatedQuest, 10);
        if (completed)
        {
            await LogLegacyMap1011Family7BanditQuest1114AutoRouteGapAsync(
                    updatedContext,
                    monster,
                    monsterFamily)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1011 family 7 Bandit quest 1114 grain reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} stack={rewardItem.StackSize} itemMode={itemMode} " +
                $"completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask LogLegacyMap1011Family7BanditQuest1114AutoRouteGapAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        const uint questId = 1_114;
        const string message = "You`ve~collected~the~bags~of~10~Bandits.~Hurry~to~report~back~to~the~ForestGuvernor!";
        const uint fallbackNpcId = 8_266;
        var finishAnchor = TryGetLegacyQuestMission(context.WorldData, questId, out var mission)
            ? mission.FinishNpcAnchors.FirstOrDefault()
            : null;
        var finishNpcId = checked((uint)(finishAnchor?.NpcId ?? (int)fallbackNpcId));
        var finishMapId = checked((uint)(finishAnchor?.MapId ?? 0));
        var finishX = checked((uint)(finishAnchor?.X ?? 0));
        var finishY = checked((uint)(finishAnchor?.Y ?? 0));

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    "map1011-family7-quest1114-autopatcher",
                    reason: $"source sends QuestGUI.SendAutoPatcher(\"{message}\") after GetFinishQuest(8266, Player.Class, 1114); modern runtime updates quest 1114 and grants the grain bag, but the optional client auto-route prompt packet is not modeled yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("message", message),
                        LegacyParityGapDiagnostics.Fact("finishMapId", finishMapId),
                        LegacyParityGapDiagnostics.Fact("finishX", finishX),
                        LegacyParityGapDiagnostics.Fact("finishY", finishY),
                        LegacyParityGapDiagnostics.Fact("finishNpcId", finishNpcId),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                    ]))
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1011Family11008BanditLeaderQuest1118Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_118;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1011 && Family.ID == 11008 quest 1118 Bandit Leader";

        if (monster.MapId != 1_011 ||
            monsterFamily.Id != 11_008 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context,
                session,
                questId,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };

        await LogLegacyMap1011Family11008BanditLeaderQuest1118AutoRouteGapAsync(
                updatedContext,
                monster,
                monsterFamily)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1011 family 11008 Bandit Leader quest 1118 objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask LogLegacyMap1011Family11008BanditLeaderQuest1118AutoRouteGapAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        const uint questId = 1_118;
        const string message = "You`ve~killed~the~Bandit~Leader.~Hurry~and~return~to~the~Bandit~Boss.";
        const uint finishMapId = 1_011;
        const uint finishX = 227;
        const uint finishY = 404;
        const uint finishNpcId = 8_272;

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    "map1011-family11008-quest1118-autopatcher",
                    reason: $"source sends QuestGUI.SendAutoPatcher(\"{message}\", 1011, 227, 404, 8272); modern runtime updates quest 1118, but the optional client auto-route prompt packet is not modeled yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("message", message),
                        LegacyParityGapDiagnostics.Fact("finishMapId", finishMapId),
                        LegacyParityGapDiagnostics.Fact("finishX", finishX),
                        LegacyParityGapDiagnostics.Fact("finishY", finishY),
                        LegacyParityGapDiagnostics.Fact("finishNpcId", finishNpcId),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                    ]))
            .ConfigureAwait(false);
    }
}
