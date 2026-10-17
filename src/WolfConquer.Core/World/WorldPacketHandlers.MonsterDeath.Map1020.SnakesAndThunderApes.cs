using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family72SnakemanBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 72)
        {
            return session;
        }

        await TryApplyLegacyMap1020Family72Quest1341FloorDropAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        return await TryApplyLegacyMap1020Family72Quest1343ObjectiveAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask TryApplyLegacyMap1020Family72Quest1341FloorDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_341;
        const uint itemId = 720_858;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 72 quest 1341 floor drop";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return;
        }

        if (!CheckLegacyMap1020Family72RewardRate(session.Character, RuntimeLegacyMap1020Family72Quest1341RateAttribute, 20))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 72 Snakeman floor drop " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=\"Core.Rate(20)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
            itemId,
            monster,
            session.Character,
            NextLegacyFloorItemUid()) with
        {
            PlusOrColor = 14,
        };
        var entry = FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, ownerOnly: true);
        if (context.FloorItems is { } floorItems)
        {
            if (!floorItems.TryAddVisibleOnGround(
                    entry,
                    DateTimeOffset.UtcNow,
                    3,
                    (mapId, x, y) => TryValidateStaticTerrainLocation(context, mapId, x, y, out _),
                    out var added))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy map 1020 family 72 Snakeman floor drop " +
                        $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"questId={questId} itemId={itemId} reason=\"AddGroundItem failed\" branch=\"{sourceBranch}\".")
                    .ConfigureAwait(false);
                return;
            }

            entry = added;
            floorItem = added.Packet;
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        "map1020-family72-quest1341-floor-drop",
                        "source map 1020 family 72 quest 1341 floor drop was sent but floor item runtime state is not loaded for pickup tracking",
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
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, session.Character, floorItem, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy map 1020 family 72 Snakeman floor drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} floorUid={floorItem.Uid} x={floorItem.X} y={floorItem.Y} " +
                $"color={floorItem.PlusOrColor} ownerOnly={entry.OwnerOnly} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family72Quest1343ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_343;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 72 quest 1343 Snakemen L63 objective";

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
        var completed = updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            IsLegacyQuestObjectiveReached(updatedQuest, 50);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You killed 50 Snakemen L63.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 72 Snakeman objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1020Family72RewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family14336Quest1356ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_356;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 14336 quest 1356 objective";

        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 14_336 ||
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
                "WorldServer applied legacy map 1020 family 14336 quest 1356 objective " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
