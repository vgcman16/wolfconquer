using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap3935StarStonePackMonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_935)
        {
            return session;
        }

        const uint starStonePackItemId = 3_600_031;
        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 3935 Star Stone Pack bounty";
        string? message = monsterFamily.Id switch
        {
            40_125 => "You've received a bounty of (Star Stone Pack) for conquering the divine Maniac!",
            40_121 or 40_122 or 40_123 or 40_124 =>
                "You've received a bounty of (Star Stone Pack) for conquering the divine crystal!",
            _ => null,
        };

        if (message is null)
        {
            return session;
        }

        if (!TryReadItemType(context.WorldData, starStonePackItemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3935-star-stone-pack",
                        reason: "source sends InterCreateItem(3600031) for map 3935 Star Stone Pack bounties, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", starStonePackItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", starStonePackItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var rewardItem = CreatePurchasedItems(session.PlayerItems, itemType, count: 1)[0];
        var updatedItems = session.PlayerItems with
        {
            InventoryItems = session.PlayerItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(message).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3935 Star Stone Pack reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} map={monster.MapId} itemId={starStonePackItemId} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3935SeniorDragonSoulMonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_935 ||
            monsterFamily.Id != 40_120)
        {
            return session;
        }

        const uint questId = 35_034;
        const uint itemId = 3_600_027;
        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 3935 && Family.ID == 40120";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        if (updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var quest) &&
            quest.Status == PlayerQuestStatus.Accepted &&
            !IsLegacyQuestObjectiveReached(quest, 1))
        {
            var finishedQuest = quest with { Status = PlayerQuestStatus.Finished };
            updatedSession = WithPlayerQuests(
                updatedSession,
                updatedSession.Character.PlayerQuests.WithQuest(finishedQuest));
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = updatedContext with { Session = updatedSession };

            await context.Connection.SendAsync(
                    LegacyQuestListPacket.CreateSingleQuestFrame(
                        LegacyQuestListMode.FinishQuest,
                        ToLegacyQuestListItem(finishedQuest)),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox(
                            "You've completed the [Scramble for Justice] mission in the realm, you've received a bounty of (Senior Dragon Soul)")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer finished legacy map 3935 Scramble for Justice quest " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3935-senior-dragon-soul",
                        reason: "source sends InterCreateItem(3600027) for map 3935 family 40120, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
            return updatedSession;
        }

        var rewardItem = CreatePurchasedItems(updatedSession.PlayerItems, itemType, count: 1)[0];
        var updatedItems = updatedSession.PlayerItems with
        {
            InventoryItems = updatedSession.PlayerItems.InventoryItems.Concat([rewardItem]).ToArray(),
        };
        updatedSession = updatedSession with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        updatedContext = updatedContext with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3935 Senior Dragon Soul reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"map={monster.MapId} itemId={itemId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool IsLegacyQuestObjectiveReached(PlayerQuestSnapshotEntry quest, uint requiredAmount)
    {
        return quest.Intentions.Count != 0 &&
            quest.Intentions[0] >= requiredAmount;
    }

    private readonly record struct LegacyMap3935BeastSpawn(
        uint MonsterId,
        string Name,
        ushort CenterX,
        ushort CenterY);

    private static async ValueTask<WorldSession> TryApplyLegacyMap3935BeastQuest35025MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_935 ||
            monsterFamily.Id is not (40_104 or 40_109 or 40_110 or 40_108))
        {
            return session;
        }

        const uint questId = 35_025;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3935 beast family quest 35025 objective";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted ||
            IsLegacyQuestObjectiveReached(quest, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3935 beast quest objective " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} status={(quest is null ? "missing" : quest.Status)} " +
                    $"objective0={(quest is { Intentions.Count: > 0 } ? quest.Intentions[0].ToString(CultureInfo.InvariantCulture) : "n/a")} " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
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
                LegacyNpcReplyPacket
                    .ForMessageBox("You've defeated the divine beast! Hurry and claim your reward from the Kingdom Mission Envoy!")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3935 beast quest objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3935BeastSpawnLoopAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_935)
        {
            return session;
        }

        const uint questId = 35_025;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3935 quest 35025 divine beast AddMapMonster";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted ||
            IsLegacyQuestObjectiveReached(quest, 1) ||
            !TrySelectLegacyMap3935BeastSpawn(session.Character, out var spawn))
        {
            return session;
        }

        if (!CheckLegacyMap3935BeastSpawnRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3935 beast spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} spawnedMonsterId={spawn.MonsterId} name={spawn.Name} " +
                    $"x={spawn.CenterX} y={spawn.CenterY} reason=\"Core.Rate(40)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var dynamicId = ReadCharacterDynamicId(session.Character);
        if (HasVisibleLegacyDynamicMonster(context.MonsterStates, 3_935, dynamicId, spawn.MonsterId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3935 beast spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} spawnedMonsterId={spawn.MonsterId} name={spawn.Name} " +
                    $"x={spawn.CenterX} y={spawn.CenterY} reason=\"visible beast already in screen\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                context,
                session,
                monster,
                monsterFamily,
                spawnedMonsterId: checked((int)spawn.MonsterId),
                mapId: 3_935,
                x: spawn.CenterX,
                y: spawn.CenterY,
                dynamicId: checked((long)dynamicId),
                sourceBranch: FormattableString.Invariant(
                    $"{sourceBranch} {spawn.Name} center=({spawn.CenterX},{spawn.CenterY})"),
                cancellationToken,
                removeOnDead: true)
            .ConfigureAwait(false);
        return session;
    }

    private static bool CheckLegacyMap3935BeastSpawnRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap3935BeastSpawnRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 40;
    }

    private static bool TrySelectLegacyMap3935BeastSpawn(
        CharacterSnapshot character,
        out LegacyMap3935BeastSpawn spawn)
    {
        var x = character.X;
        var y = character.Y;
        if (IsWithinLegacyDistance(x, y, 367, 459, 18))
        {
            spawn = new LegacyMap3935BeastSpawn(40_104, "WhiteTiger", 367, 459);
            return true;
        }

        if (IsWithinLegacyDistance(x, y, 471, 371, 18))
        {
            spawn = new LegacyMap3935BeastSpawn(40_109, "AzureDragon", 471, 371);
            return true;
        }

        if (IsWithinLegacyDistance(x, y, 371, 289, 18))
        {
            spawn = new LegacyMap3935BeastSpawn(40_108, "VermilionBird", 371, 289);
            return true;
        }

        if (IsWithinLegacyDistance(x, y, 295, 337, 18))
        {
            spawn = new LegacyMap3935BeastSpawn(40_110, "BlackTurtle", 295, 337);
            return true;
        }

        spawn = default;
        return false;
    }

    private static bool IsWithinLegacyDistance(long x1, long y1, long x2, long y2, long maxDistance)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var distanceSquared = (dx * dx) + (dy * dy);
        var maxDistanceSquared = maxDistance * maxDistance;
        return distanceSquared <= maxDistanceSquared;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3935Quest35028MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_935)
        {
            return session;
        }

        const uint questId = 35_028;
        const uint requiredKills = 50;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3935 quest 35028 enemy count";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted ||
            IsLegacyQuestObjectiveReached(quest, requiredKills))
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
            IsLegacyQuestObjectiveReached(updatedQuest, requiredKills);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You've eliminated 50 enemies. Hurry and claim a bounty from Realm Crystal.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3935 quest 35028 enemy count " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3935Quest35007MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_935)
        {
            return session;
        }

        const uint questId = 35_007;
        const uint requiredStrikePoints = 300;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3935 quest 35007 Thunder Strike points";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted ||
            IsLegacyQuestObjectiveReached(quest, requiredStrikePoints))
        {
            return session;
        }

        var strikePoints = ReadLegacyMap3935Quest35007StrikePoints(session.Character);
        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context,
                session,
                questId,
                strikePoints,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };
        var objective0 = updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            updatedQuest.Intentions.Count != 0
                ? updatedQuest.Intentions[0]
                : 0U;
        var displayedStrikePoints = Math.Min(requiredStrikePoints, objective0);

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(
                        "You've received " +
                        displayedStrikePoints.ToString(CultureInfo.InvariantCulture) +
                        " Strike Points by killing enemies fiercely. When you earn 300 Strike Points, you can claim a reward from the Kingdom Mission Envoy.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var completed = objective0 >= requiredStrikePoints;
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You've completed the [Thunder Strike] mission in the realm. Report back to the Kingdom Mission Envoy to claim your reward.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3935 quest 35007 Thunder Strike points " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} amount={strikePoints} objective0={objective0} " +
                $"displayed={displayedStrikePoints} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint ReadLegacyMap3935Quest35007StrikePoints(CharacterSnapshot character)
    {
        if (TryReadInt64Attribute(
                character.Attributes,
                RuntimeLegacyMap3935Quest35007StrikePointsAttribute,
                out var forcedStrikePoints))
        {
            return forcedStrikePoints <= 0
                ? 0
                : checked((uint)forcedStrikePoints);
        }

        return checked((uint)Random.Shared.Next(2, 6));
    }
}
