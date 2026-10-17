using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family77BirdmanL88QuestBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 77)
        {
            return session;
        }

        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 77 BirdmanL88 quest branches";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family77BirdBloodRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family77ItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_599,
                itemId: 711_445,
                maxCount: 5,
                rateAttribute: RuntimeLegacyMap1015Family77BirdBeakRateAttribute,
                ratePercent: 40,
                itemName: "Bird Beak",
                rewardMessage: "You`ve~got~a~Bird~Beak!",
                stackable: false,
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 77 quest 1599 Bird Beak reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family77ItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_601,
                itemId: 711_446,
                maxCount: 1,
                rateAttribute: RuntimeLegacyMap1015Family77SharpClawRateAttribute,
                ratePercent: 20,
                itemName: "Sharp Claw",
                rewardMessage: "You`ve~got~a~Sharp~Claw!",
                stackable: false,
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 77 quest 1601 Sharp Claw reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family77ItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_605,
                itemId: 711_449,
                maxCount: 5,
                rateAttribute: RuntimeLegacyMap1015Family77BirdmanFeatherRateAttribute,
                ratePercent: 20,
                itemName: "Birdman Feather",
                rewardMessage: "You received a Birdman Feather!",
                stackable: true,
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 77 quest 1605 Birdman Feather reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_604, out var quest1604) &&
            quest1604.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family19KillNoticeAsync(updatedContext, "You`ve killed a BirdmanL88.", cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    1_604,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 77 quest 1604 BirdmanL88 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1604 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_598, out var quest1598) &&
            quest1598.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family19KillNoticeAsync(updatedContext, "You`ve killed a BirdmanL88.", cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    1_598,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 77 quest 1598 BirdmanL88 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1598 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family77BirdBloodRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_812;
        const uint itemId = 721_906;
        const string itemName = "Bird Blood";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 77 quest 1812 Bird Blood reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1015Family77RewardRate(session.Character, RuntimeLegacyMap1015Family77BirdBloodRateAttribute, 40))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 77 BirdmanL88 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate(40)=false\" " +
                    $"branch=\"{sourceBranch}\".")
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
                    "WorldServer skipped legacy map 1015 family 77 BirdmanL88 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        if (itemCountBefore >= 10)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("You've collected enough Bird Blood!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 77 BirdmanL88 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemCountBefore={itemCountBefore} " +
                    "reason=\"item count already reached 10\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1015-family77-bird-blood",
                        reason: "source AddItemWitchStack(721906) grants Bird Blood for quest 1812, but the item type row was not loaded",
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

        var updatedItems = AddOrStackLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            itemId,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You've got some Bird Blood!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 77 BirdmanL88 item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family77ItemRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        uint maxCount,
        string rateAttribute,
        int ratePercent,
        string itemName,
        string rewardMessage,
        bool stackable,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a BirdmanL88.", cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family77RewardRate(session.Character, rateAttribute, ratePercent))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 77 BirdmanL88 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate({ratePercent})=false\" " +
                    $"branch=\"{sourceBranch}\".")
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
                    "WorldServer skipped legacy map 1015 family 77 BirdmanL88 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        if (itemCountBefore >= maxCount)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 77 BirdmanL88 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemCountBefore={itemCountBefore} " +
                    $"reason=\"item count already reached {maxCount}\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        $"map1015-family77-{itemName.ToLowerInvariant().Replace(' ', '-')}",
                        reason: $"source grants {itemName} for quest {questId.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
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

        var updatedItems = stackable
            ? AddOrStackLegacyInventoryItem(
                session.PlayerItems,
                itemType,
                itemId,
                stackCount: 1,
                out var rewardItem,
                out var itemMode)
            : AddLegacyInventoryItem(
                session.PlayerItems,
                itemType,
                stackCount: 1,
                out rewardItem,
                out itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(rewardMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 77 BirdmanL88 item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
