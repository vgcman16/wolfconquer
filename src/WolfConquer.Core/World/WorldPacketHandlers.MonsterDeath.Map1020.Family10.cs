using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family10BranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 10)
        {
            return session;
        }

        await TryApplyLegacyMap1020Family10Quest1311FloorDropAsync(
                context,
                session,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyMap1020Family10ObjectiveAsync(
                context,
                session,
                monster,
                monsterFamily,
                questId: 1_303,
                sourceBranch:
                "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 10 quest 1303 objective",
                cancellationToken)
            .ConfigureAwait(false);
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family10ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_314,
                sourceBranch:
                "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 10 quest 1314 objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        return await TryApplyLegacyMap1020Family10Quest1746DecayedHeartAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask TryApplyLegacyMap1020Family10Quest1311FloorDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_311;
        const uint itemId = 711_352;
        const uint sourceDurabilityItemId = 729_976;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 10 quest 1311 floor drop";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return;
        }

        if (!CheckLegacyMap1020Family10RewardRate(session.Character, RuntimeLegacyMap1020Family10Quest1311RateAttribute, 30))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 10 floor drop " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=\"Core.Rate(30)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadItemType(context.WorldData, sourceDurabilityItemId, out _))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1020-family10-quest1311-source-durability-item",
                        reason: $"source family 10 quest 1311 item {itemId.ToString(CultureInfo.InvariantCulture)} copies durability from item {sourceDurabilityItemId.ToString(CultureInfo.InvariantCulture)}, but that item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("sourceDurabilityItemId", sourceDurabilityItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("sourceDurabilityItemId", sourceDurabilityItemId),
                        ]))
                .ConfigureAwait(false);
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
                        "WorldServer skipped legacy map 1020 family 10 floor drop " +
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
                        "map1020-family10-quest1311-floor-drop",
                        "source map 1020 family 10 quest 1311 floor drop was sent but floor item runtime state is not loaded for pickup tracking",
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
                "WorldServer sent legacy map 1020 family 10 floor drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} sourceDurabilityItemId={sourceDurabilityItemId} " +
                $"floorUid={floorItem.Uid} x={floorItem.X} y={floorItem.Y} " +
                $"color={floorItem.PlusOrColor} ownerOnly={entry.OwnerOnly} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family10ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
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
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 10 objective " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family10Quest1746DecayedHeartAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_746;
        const uint itemId = 721_699;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 10 quest 1746 Decayed Heart";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1020Family10RewardRate(session.Character, RuntimeLegacyMap1020Family10Quest1746RateAttribute, 15))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 10 Decayed Heart reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=\"Core.Rate(15)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1020-family10-decayed-heart",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants Decayed Heart for quest {questId.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
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

        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        var updatedItems = AddLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context with { Session = updatedSession },
                updatedSession,
                questId,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You`ve~found~a~Decayed~Heart!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 10 Decayed Heart reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1020Family10RewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }
}
