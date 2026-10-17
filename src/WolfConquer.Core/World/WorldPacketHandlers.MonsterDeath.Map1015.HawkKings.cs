using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family19HawKingQuestBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 19)
        {
            return session;
        }

        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 19 HawKing quest objectives";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family19HawkClawRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family19GallbladerrRewardAsync(
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
            await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawKingL93.", cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                    updatedContext,
                    updatedSession,
                    1_630,
                    amounts: [0, 1],
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 19 quest 1630 HawKingL93 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1630 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_616, out var quest1616) &&
            quest1616.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawKing.", cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                    updatedContext,
                    updatedSession,
                    1_616,
                    amounts: [2, 1],
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 19 quest 1616 HawKing objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1616 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_611, out var quest1611) &&
            quest1611.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawKing.", cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    1_611,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 19 quest 1611 HawKing objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1611 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMap1015Family19FeatherRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family19OldBookRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family19HawkClawRewardAsync(
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
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 19 quest 1814 Hawk Claw reward";

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
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap1015Family19RewardRate(session.Character, RuntimeLegacyMap1015Family19HawkClawRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
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
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
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
                        "map1015-family19-hawk-claw",
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
                "WorldServer applied legacy map 1015 family 19 HawKing item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family19GallbladerrRewardAsync(
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
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 19 quest 1813 Gallbladerr reward";

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
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap1015Family19RewardRate(session.Character, RuntimeLegacyMap1015Family19GallbladerrRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
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
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
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
                        "map1015-family19-gallbladerr",
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
                "WorldServer applied legacy map 1015 family 19 HawKing item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
