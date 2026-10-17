using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacyQuestObjectiveSlotReached(
        IReadOnlyList<uint> intentions,
        int index,
        uint requiredAmount)
    {
        return index < intentions.Count &&
            intentions[index] >= requiredAmount;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family70GiantApeL58Quest1753SmellAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_753;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 70 quest 1753 Giant Ape smell";

        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 70 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                context,
                session,
                questId,
                [0, 0, 1],
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You,ve~collected~some~smells.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        var completed = updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            IsLegacyQuestObjectiveSlotReached(updatedQuest.Intentions, 2, 10);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("You,ve collected the smell of Gaint Ape!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 70 Giant Ape smell objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family14334Quest1749ApeLeaderSpawnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_749;
        const int repeatedMonsterId = 14_334;
        const int apeLeaderMonsterId = 14_335;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 14334 quest 1749 Ape Leader spawns";

        if (monster.MapId != 1_020 ||
            (monsterFamily.Id != 14_334 && monster.MonsterId != 14_334) ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                context,
                session,
                questId,
                [0, 1],
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        const long dynamicId = 0;
        var questMultiple = TryReadUInt32Attribute(updatedSession.Character.Attributes, out var forcedQuestMultiple, "QuestMultiple") &&
            forcedQuestMultiple > 0
                ? forcedQuestMultiple
                : 1U;
        var secondObjectiveThreshold = checked(questMultiple * 5U);
        var spawnedRepeatCount = 0;
        if (updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            IsLegacyQuestObjectiveSlotReached(updatedQuest.Intentions, 1, secondObjectiveThreshold) &&
            !IsLegacyQuestObjectiveSlotReached(updatedQuest.Intentions, 0, 1))
        {
            updatedSession = updatedSession with
            {
                Character = WithCharacterAttribute(updatedSession.Character, "QuestMultiple", 1UL),
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = updatedContext with { Session = updatedSession };

            for (var index = 0; index < 5; index++)
            {
                if (await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                            updatedContext,
                            updatedSession,
                            monster,
                            monsterFamily,
                            repeatedMonsterId,
                            mapId: 1_020,
                            x: CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X)),
                            y: CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y)),
                            dynamicId: dynamicId,
                            sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=14334 count=5 RemoveOnDead",
                            cancellationToken,
                            removeOnDead: true)
                        .ConfigureAwait(false))
                {
                    spawnedRepeatCount++;
                }
            }
        }

        var spawnedApeLeader = false;
        if (!updatedSession.Character.PlayerQuests.TryGetQuest(questId, out updatedQuest) ||
            IsLegacyQuestObjectiveSlotReached(updatedQuest.Intentions, 0, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 14334 Ape Leader spawn " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} spawnedMonsterId={apeLeaderMonsterId} reason=\"objective0 reached\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }
        else if (!CheckLegacyMap1020Family14334ApeLeaderRate(updatedSession.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 14334 Ape Leader spawn " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} spawnedMonsterId={apeLeaderMonsterId} reason=\"Core.Rate(20)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("The Ape Leader has appeared! Be extra cautious!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            spawnedApeLeader = await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                    updatedContext,
                    updatedSession,
                    monster,
                    monsterFamily,
                    apeLeaderMonsterId,
                    mapId: 1_020,
                    x: CheckedUInt16(updatedSession.Character.X, nameof(updatedSession.Character.X)),
                    y: CheckedUInt16(updatedSession.Character.Y, nameof(updatedSession.Character.Y)),
                    dynamicId: dynamicId,
                    sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=14335 Night RemoveOnDead",
                    cancellationToken,
                    removeOnDead: true)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 14334 Ape Leader spawn branch " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} spawnedMonsterId={repeatedMonsterId} spawnedCount={spawnedRepeatCount} " +
                $"apeLeaderMonsterId={apeLeaderMonsterId} apeLeaderSpawned={spawnedApeLeader} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1020Family14334ApeLeaderRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, RuntimeLegacyMap1020Family14334ApeLeaderRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 20;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family8305Quest1305MonkeyKingTailAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_305;
        const uint itemId = 720_863;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 8305 quest 1305 Monkey King Tail";

        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 8_305 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
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
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You~received~the~Monkey~King~Tail!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1020-family8305-monkey-king-tail",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants Monkey King Tail for quest {questId.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
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
            return updatedSession;
        }

        var itemCountBefore = CountLegacyInventoryItem(updatedSession.PlayerItems, itemId);
        var updatedItems = AddLegacyInventoryItem(
            updatedSession.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        updatedSession = updatedSession with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 8305 Monkey King Tail reward " +
                $"{FormatSession(updatedContext with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family69Quest1304ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_304;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 69 quest 1304 objective";

        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 69 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
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
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 69 objective " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyMap1015Family19KillNoticeAsync(
        WorldPacketHandlerContext context,
        string message,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyMap1002QuestAutoRouteGapAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        string key,
        string message,
        uint fallbackNpcId,
        uint fallbackMapId,
        uint fallbackX,
        uint fallbackY)
    {
        var finishAnchor = TryGetLegacyQuestMission(context.WorldData, questId, out var mission)
            ? mission.FinishNpcAnchors.FirstOrDefault()
            : null;
        var finishNpcId = checked((uint)(finishAnchor?.NpcId ?? (int)fallbackNpcId));
        var finishMapId = checked((uint)(finishAnchor?.MapId ?? (int)fallbackMapId));
        var finishX = checked((uint)(finishAnchor?.X ?? (int)fallbackX));
        var finishY = checked((uint)(finishAnchor?.Y ?? (int)fallbackY));

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    key,
                    reason: $"source sends QuestGUI.SendAutoPatcher(\"{message}\") for quest {questId}; modern runtime updates the quest progress, but the optional client auto-route prompt is not modeled yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("finishMapId", finishMapId),
                        LegacyParityGapDiagnostics.Fact("finishX", finishX),
                        LegacyParityGapDiagnostics.Fact("finishY", finishY),
                        LegacyParityGapDiagnostics.Fact("finishNpcId", finishNpcId),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                    ]))
            .ConfigureAwait(false);
    }
}
