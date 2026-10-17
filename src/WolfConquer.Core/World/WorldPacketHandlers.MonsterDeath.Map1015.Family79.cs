using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family79Quest1642ShotSaberRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 79)
        {
            return session;
        }

        const uint questId = 1_642;
        const uint shotSaberItemId = 711_474;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 79 quest 1642 Shot Saber reward";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You`ve killed a Banditti.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (!CheckLegacyMap1015Family79ShotSaberRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 79 quest 1642 Shot Saber reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={shotSaberItemId} reason=\"Core.Rate(40)=false\" " +
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
                    "WorldServer skipped legacy map 1015 family 79 quest 1642 Shot Saber reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={shotSaberItemId} reason=\"inventory full\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, shotSaberItemId) >= 10)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1015 family 79 quest 1642 Shot Saber reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={shotSaberItemId} reason=\"Shot Saber count already reached 10\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, shotSaberItemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1015-family79-shot-saber",
                        reason: "source AddItemWitchStack(711474) grants a Shot Saber for quest 1642, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", shotSaberItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", shotSaberItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var shotSaberCountBefore = CountLegacyInventoryItem(session.PlayerItems, shotSaberItemId);
        var updatedItems = AddOrStackLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            shotSaberItemId,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You received a Shot Saber!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 79 quest 1642 Shot Saber reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={shotSaberItemId} itemMode={itemMode} " +
                $"shotSaberCountBefore={shotSaberCountBefore} shotSaberCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, shotSaberItemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1015Family79ShotSaberRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap1015Family79ShotSaberRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 40;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family79BanditL98QuestObjectivesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 79)
        {
            return session;
        }

        const uint quest1641Id = 1_641;
        const uint quest1647Id = 1_647;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 79 BanditL98 quest objectives";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        if (updatedSession.Character.PlayerQuests.TryGetQuest(quest1641Id, out var quest1641) &&
            quest1641.Status == PlayerQuestStatus.Accepted)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You`ve killed a BanditL98.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    quest1641Id,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 79 quest 1641 BanditL98 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={quest1641Id} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(quest1647Id, out var quest1647) &&
            quest1647.Status == PlayerQuestStatus.Accepted)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You`ve killed a BanditL98.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                    updatedContext,
                    updatedSession,
                    quest1647Id,
                    amounts: [0, 1],
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1015 family 79 quest 1647 BanditL98 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={quest1647Id} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }
}
