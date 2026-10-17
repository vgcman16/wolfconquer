using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family78HawkBloodRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_624;
        const uint itemId = 720_993;
        const string itemName = "Hawk Blood";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 78 quest 1624 Hawk Blood reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawKingL93.", cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family78RewardRate(session.Character, RuntimeLegacyMap1015Family78HawkBloodRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
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
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"already owns item\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        return await TryGrantLegacyMap1015Family78InventoryRewardAsync(
                context,
                session,
                monster,
                monsterFamily,
                questId,
                itemId,
                itemName,
                rewardMessage: "You~received~some~Hawk~Blood!",
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family78HawkClawFangRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_622;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 78 quest 1622 Hawk Claw/Fang reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawKingL93.", cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family78RewardRate(session.Character, RuntimeLegacyMap1015Family78HawkClawFangRateAttribute))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemName=\"Hawk Claw/Fang\" reason=\"Core.Rate(20)=false\" " +
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
                    "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemName=\"Hawk Claw/Fang\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, 711_456) < 1)
        {
            return await TryGrantLegacyMap1015Family78InventoryRewardAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    questId,
                    itemId: 711_456,
                    itemName: "Hawk Claw",
                    rewardMessage: "You~received~1~Hawk~Claw!",
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (CountLegacyInventoryItem(session.PlayerItems, 711_457) < 1)
        {
            return await TryGrantLegacyMap1015Family78InventoryRewardAsync(
                    context,
                    session,
                    monster,
                    monsterFamily,
                    questId,
                    itemId: 711_457,
                    itemName: "Hawk Fang",
                    rewardMessage: "You~received~1~Hawk~Fang!",
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer skipped legacy map 1015 family 78 HawKingL93 item reward " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                "questId=1622 itemName=\"Hawk Claw/Fang\" reason=\"already owns both items\" " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return session;
    }

    private static async ValueTask<WorldSession> TryGrantLegacyMap1015Family78InventoryRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        string itemName,
        string rewardMessage,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        $"map1015-family78-{itemName.ToLowerInvariant().Replace(' ', '-')}",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants {itemName}, but the item type row was not loaded",
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
                "WorldServer applied legacy map 1015 family 78 HawKingL93 item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1015Family78RewardRate(CharacterSnapshot character, string attributeName)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < 20;
    }
}
