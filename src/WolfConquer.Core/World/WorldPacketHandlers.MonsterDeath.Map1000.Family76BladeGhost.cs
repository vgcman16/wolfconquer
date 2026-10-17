using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family76BladeGhostBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_000 ||
            monsterFamily.Id != 76)
        {
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family76BladeGhostItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_803,
                itemId: 721_846,
                maxCount: 5,
                rateAttribute: RuntimeLegacyMap1000Family76BladeGhostFangRateAttribute,
                ratePercent: 50,
                itemName: "Blade Ghost Fang",
                capReachedReason: "item count already reached 5",
                dialog: "You~picked~up~a~Blade~Ghost~Fang~and~put~it~in~your~inventory.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 76 quest 1803 Blade Ghost Fang reward",
                effectBeforeItem: false,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family76BladeGhostItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_801,
                itemId: 721_843,
                maxCount: 1,
                rateAttribute: RuntimeLegacyMap1000Family76StoneSpellRateAttribute,
                ratePercent: 25,
                itemName: "Stone Spell",
                capReachedReason: "Stone Spell already in inventory",
                dialog: "You~obtained~the~Stone~Spell.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 76 quest 1801 Stone Spell reward",
                effectBeforeItem: false,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family76Quest1499BladeGhostObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family76BladeGhostItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_506,
                itemId: 720_968,
                maxCount: null,
                rateAttribute: RuntimeLegacyMap1000Family76AntidoteRateAttribute,
                ratePercent: 25,
                itemName: "Antidote",
                capReachedReason: string.Empty,
                dialog: "You`ve~killed~the~Level~83~Blade~Ghost~and~obtained~an~Antidote.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 76 quest 1506 Antidote reward",
                effectBeforeItem: false,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family76BladeGhostItemRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_500,
                itemId: 711_407,
                maxCount: null,
                rateAttribute: RuntimeLegacyMap1000Family76BladeGhostEarRateAttribute,
                ratePercent: 25,
                itemName: "Blade Ghost Ear",
                capReachedReason: string.Empty,
                dialog: "You`ve~killed~a~Level~83~Blade~Ghost~and~cut~its~ear.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 76 quest 1500 Blade Ghost Ear reward",
                effectBeforeItem: true,
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family76BladeGhostItemRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        int? maxCount,
        string rateAttribute,
        int ratePercent,
        string itemName,
        string capReachedReason,
        string dialog,
        string sourceBranch,
        bool effectBeforeItem,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1000Family76RewardRate(session.Character, rateAttribute, ratePercent))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1000 family 76 Blade Ghost item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" " +
                    $"reason=\"Core.Rate({ratePercent})=false\" branch=\"{sourceBranch}\".")
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
                    "WorldServer skipped legacy map 1000 family 76 Blade Ghost item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"inventory full\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        if (maxCount is { } count &&
            itemCountBefore >= count)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1000 family 76 Blade Ghost item reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemCountBefore={itemCountBefore} " +
                    $"reason=\"{capReachedReason}\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        $"map1000-family76-{itemName.ToLowerInvariant().Replace(' ', '-')}",
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
            await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
            await context.Connection.SendAsync(effectFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(dialog).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 76 Blade Ghost item reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family76Quest1499BladeGhostObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_499;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 76 quest 1499 Level 83 Blade Ghost objective";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You`ve killed a Level 83 Blade Ghost.").ToFrameBytes(),
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
            IsLegacyQuestObjectiveReached(updatedQuest, 30);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You`ve~killed~30~Level~83~Blade~Ghosts.~Go~report~to~Spring~General~Xu.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 76 quest 1499 Level 83 Blade Ghost objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1000Family76RewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }
}
