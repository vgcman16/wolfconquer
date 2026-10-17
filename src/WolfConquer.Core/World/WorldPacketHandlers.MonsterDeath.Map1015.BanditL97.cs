using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family55Quest1820BanditLeaderSpawnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 55)
        {
            return session;
        }

        const uint questId = 1_820;
        const uint redTokenItemId = 710_562;
        const int spawnedMonsterId = 2_224;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 55 quest 1820 BanditLeaderL97 spawn";
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

        if (!updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) ||
            !IsLegacyQuestObjectiveReached(updatedQuest, 50))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 55 quest 1820 BanditL97 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} spawnedMonsterId={spawnedMonsterId} spawnedFollowup=False " +
                    $"reason=\"objective below 50\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (CountLegacyInventoryItem(updatedSession.PlayerItems, redTokenItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 55 quest 1820 BanditL97 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={redTokenItemId} spawnedMonsterId={spawnedMonsterId} spawnedFollowup=False " +
                    $"reason=\"Red Token already in inventory\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        var dynamicId = ReadCharacterDynamicId(updatedSession.Character);
        if (HasVisibleLegacyDynamicMonster(context.MonsterStates, 1_015, dynamicId, spawnedMonsterId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 55 quest 1820 BanditL97 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} spawnedMonsterId={spawnedMonsterId} spawnedFollowup=False " +
                    $"reason=\"BanditLeaderL97 already visible\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox("Bandit Leader L97 appeared on Bird Island (262,118). Get rid of him!")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        var spawnedFollowup = await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                spawnedMonsterId,
                mapId: 1_015,
                x: CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X)),
                y: CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y)),
                dynamicId: checked((long)dynamicId),
                sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=2224 SuperXp-4",
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 55 quest 1820 BanditL97 objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} spawnedMonsterId={spawnedMonsterId} spawnedFollowup={spawnedFollowup} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family55BanditL97RemainingBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 55)
        {
            return session;
        }

        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 55 BanditL97 quest branches";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_647, out var quest1647) &&
            quest1647.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family55BanditL97KillNoticeAsync(context, cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    1_647,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 55 quest 1647 BanditL97 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1647 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMap1015Family55BanditL97ItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_635,
                itemId: 711_468,
                maxCount: 1,
                rateAttribute: RuntimeLegacyMap1015Family55FoodSupplyRateAttribute,
                itemName: "Food Supply",
                rewardMessage: "You~received~a~box~of~Food~Supply!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 55 quest 1635 Food Supply reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family55BanditL97ItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_632,
                itemId: 711_467,
                maxCount: 5,
                rateAttribute: RuntimeLegacyMap1015Family55BaconRateAttribute,
                itemName: "Bacon",
                rewardMessage: "You've received a piece of Bacon!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 55 quest 1632 Bacon reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(1_633, out var quest1633) &&
            quest1633.Status == PlayerQuestStatus.Accepted)
        {
            await SendLegacyMap1015Family55BanditL97KillNoticeAsync(context, cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    1_633,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 55 quest 1633 BanditL97 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId=1633 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family55BanditL97ItemRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        uint maxCount,
        string rateAttribute,
        string itemName,
        string rewardMessage,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await SendLegacyMap1015Family55BanditL97KillNoticeAsync(context, cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family55RewardRate(session.Character, rateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 55 BanditL97 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate(20)=false\" " +
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
                    "WorldServer skipped legacy map 1015 family 55 BanditL97 item reward " +
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
                    "WorldServer skipped legacy map 1015 family 55 BanditL97 item reward " +
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
                        $"map1015-family55-{itemName.ToLowerInvariant().Replace(' ', '-')}",
                        reason: $"source AddItemWitchStack({itemId.ToString(CultureInfo.InvariantCulture)}) grants {itemName}, but the item type row was not loaded",
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
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(rewardMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 55 BanditL97 item reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1015Family55RewardRate(CharacterSnapshot character, string attributeName)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < 20;
    }

    private static async ValueTask SendLegacyMap1015Family55BanditL97KillNoticeAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You`ve killed a BanditL97.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
