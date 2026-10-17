using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family11GiantApeBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 11)
        {
            return session;
        }

        var updatedSession = await TryApplyLegacyMap1020Family11Quest1363CollectionRewardAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family11Quest1316ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family11Quest1363CollectionRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_363;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 11 quest 1363 collection reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1020Family11RewardRate(session.Character, RuntimeLegacyMap1020Family11Quest1363RateAttribute, 30))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 11 Giant Ape quest 1363 collection reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} reason=\"Core.Rate(30)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 11 Giant Ape quest 1363 collection reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} reason=\"inventory full\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var intentions = quest.Intentions;
        uint itemId;
        string message;
        uint[] increments;
        string itemName;
        if (!IsLegacyQuestObjectiveSlotReached(intentions, 0, 1))
        {
            itemId = 711_357;
            message = "You received 1 Ape Claw.";
            increments = [1, 0, 0];
            itemName = "Ape Claw";
        }
        else if (!IsLegacyQuestObjectiveSlotReached(intentions, 1, 1))
        {
            itemId = 711_358;
            message = "You received 1 Ape Bone.";
            increments = [0, 1, 0];
            itemName = "Ape Bone";
        }
        else if (!IsLegacyQuestObjectiveSlotReached(intentions, 2, 1))
        {
            itemId = 711_359;
            message = "You received 1 Ape Skin.";
            increments = [0, 0, 1];
            itemName = "Ape Skin";
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 11 Giant Ape quest 1363 collection reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} reason=\"collection complete\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        $"map1020-family11-{itemName.ToLowerInvariant().Replace(' ', '-')}",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants {itemName} for quest {questId.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
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
            return session;
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        var updatedItems = AddLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var itemSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, itemSession);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                context,
                itemSession,
                questId,
                increments,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 11 Giant Ape quest 1363 collection reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} increments={string.Join(',', increments)} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family11Quest1316ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_316;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 11 quest 1316 Giant Ape objective";

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
            IsLegacyQuestObjectiveReached(updatedQuest, 50);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You killed 50 Giant Apes.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 11 Giant Ape objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1020Family11RewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }
}
