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
    private static async ValueTask<WorldSession> TryApplyLegacyMap1002Family1PheasantQuestMonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_002 ||
            monsterFamily.Id != 1)
        {
            return session;
        }

        const uint scareApesQuestId = 1_307;
        const uint tcHuntQuestId = 824;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1002 && Family.ID == 1 Pheasant quest 1307/824";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(scareApesQuestId, out var scareApesQuest) &&
            scareApesQuest.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    scareApesQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You~killed~Pheasant~and~scared~the~Apes~away!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1002 Pheasant quest 1307 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={scareApesQuestId} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(tcHuntQuestId, out var tcHuntQuest) &&
            tcHuntQuest.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    tcHuntQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1002 Pheasant quest 824 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={tcHuntQuestId} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1002Family2TurtledoveQuest1007MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_002 ||
            monsterFamily.Id != 2)
        {
            return session;
        }

        const uint questId = 1_007;
        const uint itemId = 711_236;
        const uint sourceFinishNpcId = 7_991;
        const uint sourceFinishMapId = 1_002;
        const uint sourceFinishX = 525;
        const uint sourceFinishY = 318;
        const string autoRouteKey = "map1002-turtledove-plume-autopatcher";
        const string autoRouteMessage = "You`ve~collected~a~Turtledove~Plume.~Please~take~it~to~TC~Vice~Captain.";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1002 && Family.ID == 2 quest 1007 Turtledove Plume";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted ||
            CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
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

        var finishAnchor = TryGetLegacyQuestMission(context.WorldData, questId, out var mission)
            ? mission.FinishNpcAnchors.FirstOrDefault()
            : null;
        var finishNpcId = checked((uint)(finishAnchor?.NpcId ?? (int)sourceFinishNpcId));
        var finishMapId = checked((uint)(finishAnchor?.MapId ?? (int)sourceFinishMapId));
        var finishX = checked((uint)(finishAnchor?.X ?? (int)sourceFinishX));
        var finishY = checked((uint)(finishAnchor?.Y ?? (int)sourceFinishY));
        await LogParityGapAsync(
                updatedContext,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    autoRouteKey,
                    reason: "source sends QuestGUI.SendAutoPatcher after collecting a Turtledove Plume for quest 1007; modern runtime updates the quest objective and grants the plume, but the optional client auto-route prompt is not modeled yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("questId", questId),
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

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1002-turtledove-plume",
                        reason: "source Inventory.Add(711236) grants the Turtledove Plume for quest 1007, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
            return updatedSession;
        }

        var updatedItems = AddLegacyInventoryItem(
            updatedSession.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        updatedSession = updatedSession with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        updatedContext = updatedContext with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1002 Turtledove Plume reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemMode={itemMode} " +
                $"autoRouteMessage=\"{autoRouteMessage}\" finishMapId={finishMapId} finishX={finishX} " +
                $"finishY={finishY} finishNpcId={finishNpcId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1002Family3RobinQuestMonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_002 ||
            monsterFamily.Id != 3)
        {
            return session;
        }

        const uint artisanFeatherQuestId = 1_735;
        const uint robinEliminationQuestId = 1_009;
        const uint xpSkillQuestId = 2_365;
        const uint robinFeatherQuestId = 1_010;
        const uint robinFeatherItemId = 711_238;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1002 && Family.ID == 3 Robin quest block";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(artisanFeatherQuestId, out var artisanFeatherQuest) &&
            artisanFeatherQuest.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    artisanFeatherQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            var completed = updatedSession.Character.PlayerQuests.TryGetQuest(artisanFeatherQuestId, out var updatedQuest) &&
                IsLegacyQuestObjectiveReached(updatedQuest, 100);
            if (completed)
            {
                await context.Connection.SendAsync(
                        LegacyNpcReplyPacket
                            .ForMessageBox("You`ve~collected~enough~Feathers.~Now~you~can~go~report~to~Artisan~Luo.")
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1002 Robin quest 1735 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={artisanFeatherQuestId} completed={completed} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(robinEliminationQuestId, out var robinEliminationQuest) &&
            robinEliminationQuest.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    robinEliminationQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1002 Robin quest 1009 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={robinEliminationQuestId} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(xpSkillQuestId, out var xpSkillQuest) &&
            xpSkillQuest.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    xpSkillQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            var completed = updatedSession.Character.PlayerQuests.TryGetQuest(xpSkillQuestId, out var updatedQuest) &&
                IsLegacyQuestObjectiveReached(updatedQuest, 100);
            if (completed)
            {
                await LogLegacyMap1002QuestAutoRouteGapAsync(
                        updatedContext,
                        monster,
                        monsterFamily,
                        xpSkillQuestId,
                        key: "map1002-robin-quest2365-autopatcher",
                        message: "You`ve~killed~100~Robins!~Report~back~to~XuLiang~and~claim~your~reward.",
                        fallbackNpcId: 7_992,
                        fallbackMapId: 1_002,
                        fallbackX: 368,
                        fallbackY: 643)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1002 Robin quest 2365 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={xpSkillQuestId} completed={completed} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(robinFeatherQuestId, out var robinFeatherQuest) &&
            robinFeatherQuest.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                    updatedContext,
                    updatedSession,
                    robinFeatherQuestId,
                    amounts: [1, 1],
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            var featherCountBefore = CountLegacyInventoryItem(updatedSession.PlayerItems, robinFeatherItemId);
            var addedFeather = false;
            if (featherCountBefore < 10)
            {
                if (TryReadItemType(context.WorldData, robinFeatherItemId, out var itemType))
                {
                    var updatedItems = AddOrStackLegacyInventoryItem(
                        updatedSession.PlayerItems,
                        itemType,
                        robinFeatherItemId,
                        stackCount: 1,
                        out var rewardItem,
                        out var itemMode);
                    updatedSession = updatedSession with { PlayerItems = updatedItems };
                    context.Sessions?.Set(context.Connection, updatedSession);
                    updatedContext = updatedContext with { Session = updatedSession };
                    addedFeather = true;

                    await context.Connection.SendAsync(
                            LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await LogParityGapAsync(
                            updatedContext,
                            LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                                "itemtype.txt",
                                "map1002-robin-feather",
                                reason: "source AddItemWitchStack(711238) grants a Robin Feather for quest 1010, but the item type row was not loaded",
                                facts:
                                [
                                    LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                    LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                    LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                    LegacyParityGapDiagnostics.Fact("itemId", robinFeatherItemId),
                                ],
                                dedupeFacts:
                                [
                                    LegacyParityGapDiagnostics.Fact("itemId", robinFeatherItemId),
                                ]))
                        .ConfigureAwait(false);
                }
            }

            var completed = updatedSession.Character.PlayerQuests.TryGetQuest(robinFeatherQuestId, out var updatedQuest) &&
                IsLegacyQuestObjectiveReached(updatedQuest, 10);
            if (completed)
            {
                await LogLegacyMap1002QuestAutoRouteGapAsync(
                        updatedContext,
                        monster,
                        monsterFamily,
                        robinFeatherQuestId,
                        key: "map1002-robin-feather-autopatcher",
                        message: "You`ve~collected~10~Robin~Feathers.~Please~take~them~to~Artisan~Luo.",
                        fallbackNpcId: 4_467,
                        fallbackMapId: 1_002,
                        fallbackX: 342,
                        fallbackY: 640)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1002 Robin quest 1010 feather progress " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={robinFeatherQuestId} itemId={robinFeatherItemId} " +
                    $"featherCountBefore={featherCountBefore} featherCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, robinFeatherItemId)} " +
                    $"addedFeather={addedFeather} completed={completed} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1013DynamicFamily14333Quest1748MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_013 ||
            ReadCharacterDynamicId(session.Character) == 0 ||
            monsterFamily.Id != 14_333)
        {
            return session;
        }

        const uint questId = 1_748;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1013 && killer.Player.DynamicID != 0 && Family.ID == 14333 quest 1748";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
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
        var completed = updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            IsLegacyQuestObjectiveReached(updatedQuest, 5);
        if (completed)
        {
            updatedSession = await SendLegacyMonsterDeathTeleportAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    mapId: 1_002,
                    x: 299,
                    y: 244,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("You`ve~returned~to~Royal~Doctor~Li.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1013 dynamic Frantic Monkeys quest 1748 objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
