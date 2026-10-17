using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyKingDoomCrystalsFirstNpcId = 19_121;
    private const uint LegacyKingDoomCrystalsLastNpcId = 19_125;
    private const uint LegacyKingDoomCrystalsQuestId = 35_028;
    private const uint LegacyKingDoomCrystalsRequiredKills = 50;
    private const string LegacyKingDoomCrystalsSourceMethod = "NpcHandler.KingDoomCrystals";
    private const string LegacyKingDoomCrystalsClaimedText =
        "You've already claimed a bounty today. No more to claim.";
    private const string LegacyKingDoomCrystalsInventoryFullText =
        "Please make 1 more space in your inventory.";
    private const string LegacyKingDoomCrystalsCompleteText =
        "You've restored peace to the Realm by killing 50 enemy soldiers, and received a Realm Crystal Fragment.";
    private const string LegacyKingDoomCrystalsNeedKillsText =
        "To claim a bounty, you should kill 50 enemies in the Realm.";

    private static async ValueTask<bool> TryHandleLegacyKingDoomCrystalsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyKingDoomCrystalsNpc(npcId))
        {
            return false;
        }

        if (optionId != 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc KingDoomCrystals option " +
                    $"{FormatSession(context)} npcId={npcId} option={optionId} " +
                    $"source={LegacyKingDoomCrystalsSourceMethod}; source returns for nonzero options.")
                .ConfigureAwait(false);
            return true;
        }

        if (!session.Character.PlayerQuests.TryGetQuest(LegacyKingDoomCrystalsQuestId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(LegacyKingDoomCrystalsClaimedText).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc KingDoomCrystals claimed branch " +
                    $"{FormatSession(context)} npcId={npcId} questId={LegacyKingDoomCrystalsQuestId} " +
                    $"source={LegacyKingDoomCrystalsSourceMethod}.")
                .ConfigureAwait(false);
            return true;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(LegacyKingDoomCrystalsInventoryFullText).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc KingDoomCrystals claim " +
                    $"{FormatSession(context)} npcId={npcId} questId={LegacyKingDoomCrystalsQuestId} " +
                    $"source={LegacyKingDoomCrystalsSourceMethod}; inventory is full.")
                .ConfigureAwait(false);
            return true;
        }

        if (!IsLegacyKingDoomCrystalsReady(quest))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(LegacyKingDoomCrystalsNeedKillsText).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc KingDoomCrystals claim " +
                    $"{FormatSession(context)} npcId={npcId} questId={LegacyKingDoomCrystalsQuestId} " +
                    $"objective0={GetLegacyQuestObjective0(quest)} source={LegacyKingDoomCrystalsSourceMethod}; " +
                    "kingdom enemy objective is incomplete.")
                .ConfigureAwait(false);
            return true;
        }

        var finishedQuest = quest with { Status = PlayerQuestStatus.Finished };
        var updatedSession = WithPlayerQuests(
            session,
            session.Character.PlayerQuests.WithQuest(finishedQuest));
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyPlayerQuestsAsync(
                updatedContext,
                updatedSession,
                LegacyKingDoomCrystalsSourceMethod,
                $"king-doom-crystals-claim npcId={npcId}",
                finishedQuest.Uid,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyQuestListPacket.CreateSingleQuestFrame(
                    LegacyQuestListMode.FinishQuest,
                    ToLegacyQuestListItem(finishedQuest)),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyKingDoomCrystalsCompleteText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc KingDoomCrystals claim " +
                $"{FormatSession(updatedContext)} npcId={npcId} questId={LegacyKingDoomCrystalsQuestId} " +
                $"objective0={GetLegacyQuestObjective0(quest)} source={LegacyKingDoomCrystalsSourceMethod}; " +
                "source-complete QuestGUI.FinishQuest has no inventory reward side effects.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyKingDoomCrystalsNpc(uint npcId)
    {
        return npcId is >= LegacyKingDoomCrystalsFirstNpcId and <= LegacyKingDoomCrystalsLastNpcId;
    }

    private static bool IsLegacyKingDoomCrystalsReady(PlayerQuestSnapshotEntry quest)
    {
        return quest.Intentions.Count > 0 &&
            quest.Intentions[0] >= LegacyKingDoomCrystalsRequiredKills;
    }

    private static uint GetLegacyQuestObjective0(PlayerQuestSnapshotEntry quest)
    {
        return quest.Intentions.Count > 0 ? quest.Intentions[0] : 0;
    }
}
