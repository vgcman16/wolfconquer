using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family84BandittiItemRewardsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 84)
        {
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        updatedSession = await TryApplyLegacyMap1015Family84BandittiItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_651,
                itemId: 711_476,
                rateAttribute: RuntimeLegacyMap1015Family84YarnBallRateAttribute,
                itemName: "Yarn Ball",
                rewardMessage: "You received 1 Yarn Ball!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 84 quest 1651 Yarn Ball reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1015Family84BandittiItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_652,
                itemId: 711_477,
                rateAttribute: RuntimeLegacyMap1015Family84FlowerWineRateAttribute,
                itemName: "Flower Wine",
                rewardMessage: "You received Flower Wine!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 84 quest 1652 Flower Wine reward",
                cancellationToken)
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family84BandittiItemRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
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

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You`ve killed a Banditti.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family84RewardRate(session.Character, rateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 84 Banditti item reward " +
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
                    "WorldServer skipped legacy map 1015 family 84 Banditti item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 84 Banditti item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"item already in inventory\" " +
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
                        $"map1015-family84-{itemName.ToLowerInvariant().Replace(' ', '-')}",
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
                "WorldServer applied legacy map 1015 family 84 Banditti item reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1015Family84RewardRate(CharacterSnapshot character, string attributeName)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < 40;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family84Quest1650AndRoyalSwordAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 84)
        {
            return session;
        }

        const uint bandittiQuestId = 1_650;
        const uint royalSwordQuestId = 1_645;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 84 quest 1650/1645 Banditti branches";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(bandittiQuestId, out var bandittiQuest) &&
            bandittiQuest.Status == PlayerQuestStatus.Accepted)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You`ve killed a Banditti.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    bandittiQuestId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 84 quest 1650 Banditti objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={bandittiQuestId} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        updatedSession = await TryApplyLegacyMap1015Family84BandittiItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: royalSwordQuestId,
                itemId: 711_475,
                rateAttribute: RuntimeLegacyMap1015Family84RoyalSwordRateAttribute,
                itemName: "Royal Sword",
                rewardMessage: "You received the Royal Sword!",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 84 quest 1645 Royal Sword reward",
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }
}
