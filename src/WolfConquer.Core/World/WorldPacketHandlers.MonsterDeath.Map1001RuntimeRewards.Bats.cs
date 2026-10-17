using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1001BatEssenceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_832;
        const uint itemId = 721_877;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1001 && Family.ID == 56 && Quest 1832 Accepted";

        if (monsterFamily.Id != 56 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You~inventory~is~full.~You`ll~need~to~make~some~room,~first.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 BatEssence reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} questId={questId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap1001BatEssenceRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 BatEssence reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} questId={questId} reason=rate-miss branch=\"{sourceBranch}; Core.Rate(20)=false\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 5)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 BatEssence reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} questId={questId} count=5 reason=already-collected branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map-1001-bat-essence",
                        reason: "source grants BatEssence item 721877 on monster death, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
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
        var itemSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, itemSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        itemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You found the BatEssence from the Tomb Bats.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context with { Session = itemSession },
                itemSession,
                questId,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1001 BatEssence reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} itemId={itemId} mode={itemMode} stack={rewardItem.StackSize} " +
                $"branch=\"{sourceBranch}; Core.Rate(20)=true\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1001TombBatKeyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_829;
        const uint blockingItemId = 721_870;
        const uint itemId = 721_876;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1001 && Family.ID == 20 && Quest 1829 Accepted";

        if (monsterFamily.Id != 20 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("You~inventory~is~full.~You`ll~need~to~make~some~room,~first.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Tomb Bat key reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} questId={questId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap1001TombBatKeyRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Tomb Bat key reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} questId={questId} reason=rate-miss branch=\"{sourceBranch}; Core.Rate(20)=false\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, blockingItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Tomb Bat key reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} blockingItemId={blockingItemId} questId={questId} " +
                    $"reason=blocking-item-owned branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 5)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("Sorry,~but~you`ve~already~had~5~Keys.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Tomb Bat key reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} questId={questId} count=5 reason=already-collected branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map-1001-tomb-bat-key",
                        reason: "source grants Tomb Bat key item 721876 on monster death, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
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
        var itemSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, itemSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        itemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var objectiveSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context with { Session = itemSession },
                itemSession,
                questId,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You~received~a~Key~from~the~Tomb~Bat.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1001 Tomb Bat key reward " +
                $"{FormatSession(context with { Session = objectiveSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} itemId={itemId} mode={itemMode} stack={rewardItem.StackSize} " +
                $"branch=\"{sourceBranch}; Core.Rate(20)=true\".")
            .ConfigureAwait(false);
        return objectiveSession;
    }

    private static bool CheckLegacyMap1001BatEssenceRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap1001BatEssenceRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 20;
    }

    private static bool CheckLegacyMap1001TombBatKeyRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap1001TombBatKeyRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 20;
    }

    private static bool HasLegacyMap1001FerventPill(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var active,
                RuntimeLegacyOnFerentPillAttribute,
                RuntimeLegacyOnFerventPillAttribute)
            && active;
    }
}
