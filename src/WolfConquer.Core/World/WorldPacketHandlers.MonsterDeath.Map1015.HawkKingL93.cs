using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family78HawKingL93QuestBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 78)
        {
            return session;
        }

        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 78 HawKingL93 quest branches";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family78HawkClawRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family78GallbladerrRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_630, out var quest1630) &&
            quest1630.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family19KillNoticeAsync(updatedContext, "You`ve killed a HawKingL93.", cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    1_630,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 78 quest 1630 HawKingL93 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1630 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_628, out var quest1628) &&
            quest1628.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family19KillNoticeAsync(updatedContext, "You`ve killed a HawKingL93.", cancellationToken)
                .ConfigureAwait(false);
            if (CheckLegacyMap1015Family78RewardRate(updatedSession.Character, RuntimeLegacyMap1015Family78Quest1628RateAttribute))
            {
                updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                        updatedContext,
                        updatedSession,
                        1_628,
                        amount: 1,
                        sourceBranch,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedContext = updatedContext with { Session = updatedSession };
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy map 1015 family 78 quest 1628 HawKingL93 objective " +
                        $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"questId=1628 branch=\"{sourceBranch}\".")
                    .ConfigureAwait(false);
            }
            else
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy map 1015 family 78 quest 1628 HawKingL93 objective " +
                        $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"questId=1628 reason=\"Core.Rate(20)=false\" branch=\"{sourceBranch}\".")
                    .ConfigureAwait(false);
            }
        }

        updatedSession = await TryApplyLegacyMap1015Family78HawkBloodRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family78HawkClawFangRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_621, out var quest1621) &&
            quest1621.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family19KillNoticeAsync(updatedContext, "You`ve killed a HawKingL93.", cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    1_621,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 78 quest 1621 HawKingL93 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1621 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family78HawkClawRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_814;
        const uint itemId = 721_910;
        const string itemName = "Hawk Claw";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 78 quest 1814 Hawk Claw reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap1015Family78RewardRate(session.Character, RuntimeLegacyMap1015Family78HawkClawRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate(20)=false\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        if (itemCountBefore >= 5)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You've got 5 Hawk Claws! Hurry to deliver them to Doctor Know-it-All!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemCountBefore={itemCountBefore} " +
                    "reason=\"item count already reached 5\" " +
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
                        "map1015-family78-hawk-claw",
                        reason: "source AddItemWitchStack(721910) grants a Hawk Claw for quest 1814, but the item type row was not loaded",
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
                LegacyNpcReplyPacket.ForMessageBox("You've got a Hawk Claw!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 78 HawKingL93 item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family78GallbladerrRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_813;
        const uint itemId = 721_909;
        const uint blockingItemId = 721_908;
        const string itemName = "Gallbladerr";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 78 quest 1813 Gallbladerr reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1015Family78RewardRate(session.Character, RuntimeLegacyMap1015Family78GallbladerrRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate(20)=false\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1 ||
            CountLegacyInventoryItem(session.PlayerItems, blockingItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" " +
                    "reason=\"already owns Gallbladerr gate item\" " +
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
                        "map1015-family78-gallbladerr",
                        reason: "source Inventory.Add(721909) grants a Gallbladerr for quest 1813, but the item type row was not loaded",
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

        var updatedItems = AddLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You've got a Gallbladerr!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 78 HawKingL93 item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
