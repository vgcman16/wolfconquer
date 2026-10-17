using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1002Family4ApparitionQuestMonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_002 ||
            monsterFamily.Id != 4)
        {
            return session;
        }

        const uint handkerchiefQuestId = 1_013;
        const uint spiritQuestId = 1_016;
        const uint handkerchiefItemId = 711_241;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1002 && Family.ID == 4 Apparition quest block";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(handkerchiefQuestId, out var handkerchiefQuest) &&
            handkerchiefQuest.Status == PlayerQuestStatus.Accepted &&
            CountLegacyInventoryItem(updatedSession.PlayerItems, handkerchiefItemId) < 1)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    handkerchiefQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };

            await LogLegacyMap1002QuestAutoRouteGapAsync(
                    updatedContext,
                    monster,
                    monsterFamily,
                    handkerchiefQuestId,
                    key: "map1002-apparition-handkerchief-autopatcher",
                    message: "You`ve~killed~the~Apparition~and~received~a~Handkerchief!",
                    fallbackNpcId: 7_993,
                    fallbackMapId: 1_002,
                    fallbackX: 495,
                    fallbackY: 622)
                .ConfigureAwait(false);

            if (!TryReadItemType(context.WorldData, handkerchiefItemId, out var itemType))
            {
                await LogParityGapAsync(
                        updatedContext,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            "itemtype.txt",
                            "map1002-apparition-handkerchief",
                            reason: "source Inventory.Add(711241) grants the Handkerchief for quest 1013, but the item type row was not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("itemId", handkerchiefItemId),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("itemId", handkerchiefItemId),
                            ]))
                    .ConfigureAwait(false);
            }
            else
            {
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
                        "WorldServer applied legacy map 1002 Apparition quest 1013 Handkerchief reward " +
                        $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"questId={handkerchiefQuestId} itemId={handkerchiefItemId} itemMode={itemMode} branch=\"{sourceBranch}\".")
                    .ConfigureAwait(false);
            }
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(spiritQuestId, out var spiritQuest) &&
            spiritQuest.Status == PlayerQuestStatus.Accepted)
        {
            const string spiritMessage =
                "You've collected spirit of an Apparition and stored it in Sipirit Container.";
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(spiritMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    spiritQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            var completed = updatedSession.Character.PlayerQuests.TryGetQuest(spiritQuestId, out var updatedSpiritQuest) &&
                IsLegacyQuestObjectiveReached(updatedSpiritQuest, 10);
            if (completed)
            {
                await LogLegacyMap1002QuestAutoRouteGapAsync(
                        updatedContext,
                        monster,
                        monsterFamily,
                        spiritQuestId,
                        key: "map1002-apparition-spirit-autopatcher",
                        message: "You`ve~collected~the~spirits~of~10~Apparitions.~Hurry~to~report~back~to~the~Fortuneteller!",
                        fallbackNpcId: 600_050,
                        fallbackMapId: 1_002,
                        fallbackX: 489,
                        fallbackY: 622)
                    .ConfigureAwait(false);
            }

            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1002 Apparition quest 1016 spirit objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={spiritQuestId} completed={completed} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }
}
