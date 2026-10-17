using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40903Quest3634MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_903)
        {
            return session;
        }

        const uint questId = 3_634;
        const uint requiredKills = 10;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40903 quest 3634";
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
            IsLegacyQuestObjectiveReached(updatedQuest, requiredKills);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You've eliminated enough number of Anger Rats. Hurry and report back to Chong Yan Elder!")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Anger Rat quest objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40900Quest3636MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_900)
        {
            return session;
        }

        const uint questId = 3_636;
        const uint itemId = 3_008_752;
        const ushort requiredFragments = 8;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40900 quest 3636 Rune Fragment";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap3998Family40900RuneFragmentRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3998 Rune Fragment reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} reason=\"Core.Rate(45)=false\" branch=\"{sourceBranch}\".")
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
                    "WorldServer skipped legacy map 3998 Rune Fragment reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3998-rune-fragment",
                        reason: "source AddItemWitchStack(3008752) grants a Rune Fragment for quest 3636, but the item type row was not loaded",
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
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (CountLegacyInventoryItem(updatedSession.PlayerItems, itemId) >= requiredFragments)
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "QuestGUI.SendAutoPatcher",
                        "map3998-rune-fragment-autopatcher",
                        reason: "source sends QuestGUI.SendAutoPatcher after collecting eight Rune Fragments for quest 3636; modern runtime grants/stacks the fragment but the optional client auto-route prompt is not modeled yet",
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
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Rune Fragment reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} stack={rewardItem.StackSize} itemMode={itemMode} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap3998Family40900RuneFragmentRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap3998Family40900RuneFragmentRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 45;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40902Quest3638MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_902)
        {
            return session;
        }

        const uint questId = 3_638;
        const uint itemId = 3_008_750;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40902 quest 3638 Violet Bat King hammer";
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
                    "WorldServer skipped legacy map 3998 Violet Bat King hammer reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        var addedItem = false;
        if (CountLegacyInventoryItem(updatedSession.PlayerItems, itemId) < 1)
        {
            if (!TryReadItemType(context.WorldData, itemId, out var itemType))
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            "itemtype.txt",
                            "map3998-violet-bat-king-hammer",
                            reason: "source Inventory.Add(3008750) grants the ancient-style hammer for quest 3638, but the item type row was not loaded",
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

            var updatedItems = AddLegacyInventoryItem(
                updatedSession.PlayerItems,
                itemType,
                stackCount: 1,
                out var rewardItem,
                out var itemMode);
            updatedSession = updatedSession with { PlayerItems = updatedItems };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };
            addedItem = true;

            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await LogParityGapAsync(
                updatedContext,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    "map3998-violet-bat-king-hammer-autopatcher",
                    reason: "source sends QuestGUI.SendAutoPatcher after the Violet Bat King hammer drops for quest 3638; modern runtime grants/preserves the hammer but the optional client auto-route prompt is not modeled yet",
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
                "WorldServer applied legacy map 3998 Violet Bat King hammer reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} addedItem={addedItem} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40904Quest3641MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_904)
        {
            return session;
        }

        const uint questId = 3_641;
        const uint requiredKills = 15;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40904 quest 3641 Lava Scorpion";
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
            IsLegacyQuestObjectiveReached(updatedQuest, requiredKills);
        if (completed)
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "QuestGUI.SendAutoPatcher",
                        "map3998-lava-scorpion-autopatcher",
                        reason: "source sends QuestGUI.SendAutoPatcher after enough Lava Scorpions are defeated for quest 3641; modern runtime updates the quest objective but the optional client auto-route prompt is not modeled yet",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ]))
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Lava Scorpion quest objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
