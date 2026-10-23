using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
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


        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, session))
            return session;

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
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
            return session;
        }


        if (itemType is null || itemType.Id != itemId || itemType.LegacyDurability > ushort.MaxValue)
        {
            await LogLegacyPlumeRewardRefusedAsync(context, "itemRewardMetadataInvalid").ConfigureAwait(false);
            return session;
        }
        if (!TryPrepareLegacyPlumeObjective(session, quest, out var questSession, out var objectiveFrame))
        {
            await LogLegacyPlumeRewardRefusedAsync(context, "questRewardPreparationInvalid").ConfigureAwait(false);
            return session;
        }

        var finishAnchor = TryGetLegacyQuestMission(context.WorldData, questId, out var mission)
            ? mission.FinishNpcAnchors.FirstOrDefault()
            : null;
        var finishNpcId = checked((uint)(finishAnchor?.NpcId ?? (int)sourceFinishNpcId));
        var finishMapId = checked((uint)(finishAnchor?.MapId ?? (int)sourceFinishMapId));
        var finishX = checked((uint)(finishAnchor?.X ?? (int)sourceFinishX));
        var finishY = checked((uint)(finishAnchor?.Y ?? (int)sourceFinishY));
        PlayerItemSnapshot updatedItems;
        PlayerItemSnapshotItem rewardItem;
        ushort itemMode;
        byte[] rewardFrame;
        try
        {
            updatedItems = AddLegacyInventoryItem(session.PlayerItems, itemType,
                stackCount: 1, out rewardItem, out itemMode);
            rewardFrame = LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes();
        }
        catch (OverflowException)
        {
            await LogLegacyPlumeRewardRefusedAsync(context, "itemRewardPreparationOverflow").ConfigureAwait(false);
            return session;
        }

        var updatedSession = questSession with { PlayerItems = updatedItems };
        cancellationToken.ThrowIfCancellationRequested();
        if (!sessions.TryCompareAndSet(context.Connection, session, updatedSession))
            return session;

        // The accepted CAS is the in-memory commit. This existing save helper
        // reports ordinary I/O failures as a persistence gap; it does not undo
        // the already committed combined reward, nor constitute an outbox.
        var updatedContext = context with { Session = updatedSession };
        await PersistLegacyPlayerQuestsAsync(updatedContext, updatedSession, sourceBranch,
                "monster-death-quest-objective-increment", questId, cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(objectiveFrame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy monster death quest objective " +
                $"{FormatSession(updatedContext)} questId={questId} amount=1 " +
                $"objective0={updatedSession.Character.PlayerQuests.Quests.First(entry => entry.Uid == questId).Intentions[0]} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);

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


        await context.Connection.SendAsync(rewardFrame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1002 Turtledove Plume reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemMode={itemMode} " +
                $"autoRouteMessage=\"{autoRouteMessage}\" finishMapId={finishMapId} finishX={finishX} " +
                $"finishY={finishY} finishNpcId={finishNpcId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static ValueTask LogLegacyPlumeRewardRefusedAsync(
        WorldPacketHandlerContext context, string reason) =>
        new(context.Log.WriteLineAsync(
            $"WorldServer refused legacy Turtledove Plume reward {FormatSession(context)} reason={reason}."));
}
