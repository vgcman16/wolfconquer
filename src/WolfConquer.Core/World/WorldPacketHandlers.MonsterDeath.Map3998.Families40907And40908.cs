using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40908Quest3644MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_908)
        {
            return session;
        }

        const uint questId = 3_644;
        const uint itemId = 3_008_742;
        const ushort requiredStones = 50;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40908 quest 3644 Building Stone";
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
                    "WorldServer skipped legacy map 3998 Building Stone reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        var existingCount = CountLegacyInventoryItem(updatedSession.PlayerItems, itemId);
        if (existingCount >= requiredStones)
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "QuestGUI.SendAutoPatcher",
                        "map3998-building-stone-autopatcher",
                        reason: "source sends QuestGUI.SendAutoPatcher only when the player already has fifty Building Stones before the family 40908 kill; modern runtime preserves the pre-count branch but the optional client auto-route prompt is not modeled yet",
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
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3998 Building Stone auto-route check " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} existingCount={existingCount} addedItem=False branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3998-building-stone",
                        reason: "source AddItemWitchStack(3008742) grants a Building Stone for quest 3644, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var updatedItems = AddOrStackLegacyInventoryItem(
            updatedSession.PlayerItems,
            itemType,
            itemId,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        updatedSession = updatedSession with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Building Stone reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} existingCount={existingCount} stack={rewardItem.StackSize} " +
                $"itemMode={itemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40907Quest3648MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_907)
        {
            return session;
        }

        const uint questId = 3_648;
        const uint itemId = 3_008_748;
        const ushort requiredOres = 100;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40907 quest 3648 Star Ore";
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
                    "WorldServer skipped legacy map 3998 Star Ore reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        var existingCount = CountLegacyInventoryItem(updatedSession.PlayerItems, itemId);
        if (existingCount >= requiredOres)
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "QuestGUI.SendAutoPatcher",
                        "map3998-star-ore-autopatcher",
                        reason: "source sends QuestGUI.SendAutoPatcher only when the player already has one hundred Star Ores before the family 40907 kill; modern runtime preserves the pre-count branch but the optional client auto-route prompt is not modeled yet",
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
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3998 Star Ore auto-route check " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} existingCount={existingCount} addedItem=False branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3998-star-ore",
                        reason: "source AddItemWitchStack(3008748) grants a Star Ore for quest 3648, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var updatedItems = AddOrStackLegacyInventoryItem(
            updatedSession.PlayerItems,
            itemType,
            itemId,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        updatedSession = updatedSession with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Star Ore reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} existingCount={existingCount} stack={rewardItem.StackSize} " +
                $"itemMode={itemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
