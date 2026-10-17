using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family19FeatherRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_618;
        const uint itemId = 711_454;
        const string itemName = "Feather";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 19 quest 1618 Feather reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawKing.", cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family19RewardRate(session.Character, RuntimeLegacyMap1015Family19FeatherRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
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
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        if (itemCountBefore >= 5)
        {
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
                        "map1015-family19-feather",
                        reason: "source Inventory.Add(711454) grants a Feather for quest 1618, but the item type row was not loaded",
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
                LegacyMsgMessagePacket.SystemTalk("You`ve~got~a~Feather!").ToFrameBytes(),
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

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family19OldBookRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_610;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 19 quest 1610 OldBook reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawKing.", cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family19RewardRate(session.Character, RuntimeLegacyMap1015Family19OldBookRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemName=\"OldBook\" reason=\"Core.Rate(20)=false\" " +
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
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemName=\"OldBook\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var itemId = 720_987U;
        var itemName = "OldBook1";
        var rewardMessage = "You`ve~got~a~OldBook1!";
        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            itemId = 720_988U;
            itemName = "OldBook2";
            rewardMessage = "You`ve~got~a~OldBook2!";
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            itemId = 720_989U;
            itemName = "OldBook3";
            rewardMessage = "You`ve~got~a~OldBook3!";
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 19 HawKing item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemName=\"OldBook\" reason=\"all old books already owned\" " +
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
                        $"map1015-family19-{itemName.ToLowerInvariant()}",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants {itemName} for quest 1610, but the item type row was not loaded",
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
                LegacyMsgMessagePacket.SystemTalk(rewardMessage).ToFrameBytes(),
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

    private static bool CheckLegacyMap1015Family19RewardRate(CharacterSnapshot character, string attributeName)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < 20;
    }
}
