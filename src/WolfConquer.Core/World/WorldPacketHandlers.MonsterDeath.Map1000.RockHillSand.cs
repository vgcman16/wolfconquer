using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family14SandMonsterBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_000 ||
            monsterFamily.Id != 14)
        {
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family14InventoryRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_458,
                itemId: 711_341,
                rateAttribute: RuntimeLegacyMap1000Family14SandbagRateAttribute,
                ratePercent: 30,
                itemName: "Sandbag",
                dialog: "You`ve~killed~the~Sand~Monster~and~received~a~Sandbag.",
                effectBeforeItem: false,
                effectAfterItem: true,
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 14 quest 1458 Sandbag reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family14InventoryRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_501,
                itemId: 711_410,
                rateAttribute: RuntimeLegacyMap1000Family14SandPillRateAttribute,
                ratePercent: 40,
                itemName: "Sand Pill",
                dialog: "You`ve~killed~the~Sand~Monster~and~received~a~Sand~Pill.",
                effectBeforeItem: true,
                effectAfterItem: false,
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 14 quest 1501 Sand Pill reward",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family14ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_464,
                requiredKills: 40,
                completionDialog: "You`ve~killed~40~Sand~Monsters.~After~you~kill~another~10~Level~68~Sand~Monsters,~you~can~go~report~to~Ke~Yulun.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 14 quest 1464 Sand Monster objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family14ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_451,
                requiredKills: 50,
                completionDialog: "You`ve~killed~50~Sand~Monsters.~Go~report~to~the~DC~Vice~Captain.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 14 quest 1451 Sand Monster objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family14InventoryRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_453,
                itemId: 711_338,
                rateAttribute: RuntimeLegacyMap1000Family14Quest1453RateAttribute,
                ratePercent: 20,
                itemName: "Quest 1453 item",
                dialog: null,
                effectBeforeItem: false,
                effectAfterItem: false,
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 14 quest 1453 item reward",
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family14InventoryRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        string rateAttribute,
        int ratePercent,
        string itemName,
        string? dialog,
        bool effectBeforeItem,
        bool effectAfterItem,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1000Family14RewardRate(session.Character, rateAttribute, ratePercent))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1000 family 14 Sand Monster inventory reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate({ratePercent})=false\" " +
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
                    "WorldServer skipped legacy map 1000 family 14 Sand Monster inventory reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
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
                        $"map1000-family14-{itemName.ToLowerInvariant().Replace(' ', '-')}",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants {itemName} for quest {questId.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
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

        var effectFrame = LegacyStringPacket
            .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "relive1")
            .ToFrameBytes();
        var itemFrame = LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes();
        if (effectBeforeItem)
        {
            await context.Connection.SendAsync(effectFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);

        if (effectAfterItem)
        {
            await context.Connection.SendAsync(effectFrame, cancellationToken).ConfigureAwait(false);
        }

        if (dialog is not null)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(dialog).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 14 Sand Monster inventory reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family14ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint requiredKills,
        string completionDialog,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You've~killed~a~Sand~Monster.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
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
                    LegacyNpcReplyPacket.ForMessageBox(completionDialog).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 14 Sand Monster objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1000Family14RewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }

}
