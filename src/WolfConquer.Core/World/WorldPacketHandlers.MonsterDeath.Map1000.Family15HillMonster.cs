using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family15HillMonsterBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_000 ||
            monsterFamily.Id != 15)
        {
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family15HeartRewardAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family15ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_476,
                requiredFirstObjective: 40,
                requiredSecondObjective: 10,
                sendKillNotice: true,
                completionDialog: "You`ve~killed~40~Hill~Monsters~and~10~Hill~Monsters~L73.~Go~report~to~Han~Cheng.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 15 quest 1476 Hill Monster first objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1000Family15ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_466,
                requiredFirstObjective: 50,
                requiredSecondObjective: null,
                sendKillNotice: true,
                completionDialog: "You`ve~killed~50~Hill~Monsters.~Go~report~to~General~ZhuGe.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 15 quest 1466 Hill Monster objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        await TryApplyLegacyMap1000Family15FloorDropAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_467,
                itemId: 711_344,
                rateAttribute: RuntimeLegacyMap1000Family15HammerRateAttribute,
                itemName: "Hammer",
                dialog: "You`ve~killed~the~Hill~Monster~and~received~a~Hammer.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 15 quest 1467 Hammer floor drop",
                cancellationToken)
            .ConfigureAwait(false);

        await TryApplyLegacyMap1000Family15FloorDropAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_474,
                itemId: 711_415,
                rateAttribute: RuntimeLegacyMap1000Family15HeavyAxeRateAttribute,
                itemName: "Heavy Axe",
                dialog: "You`ve~killed~the~monster~and~received~a~Heavy~Axe.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 15 quest 1474 Heavy Axe floor drop",
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyMap1000Family15ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_465,
                requiredFirstObjective: 20,
                requiredSecondObjective: null,
                sendKillNotice: false,
                completionDialog: "You`ve~killed~20~Hill~Monsters.~Go~report~to~General~ZhuGe.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 15 quest 1465 Hill Monster objective",
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family15HeartRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_501;
        const uint itemId = 711_411;
        const string itemName = "Hill Monster Heart";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 15 quest 1501 Hill Monster Heart reward";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1000Family15RewardRate(session.Character, RuntimeLegacyMap1000Family15HeartRateAttribute, 40))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1000 family 15 Hill Monster inventory reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate(40)=false\" " +
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
                    "WorldServer skipped legacy map 1000 family 15 Hill Monster inventory reward " +
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
                        "map1000-family15-hill-monster-heart",
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

        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "relive1").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("You`ve~killed~the~Hill~Monster~and~received~a~Hill~Monster`s~Heart.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 15 Hill Monster inventory reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family15ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint requiredFirstObjective,
        uint? requiredSecondObjective,
        bool sendKillNotice,
        string completionDialog,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (sendKillNotice)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You've killed a Hill Monster.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
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
            (requiredSecondObjective is { } secondObjective
                ? updatedQuest.Intentions.Count >= 2 &&
                    updatedQuest.Intentions[0] >= requiredFirstObjective &&
                    updatedQuest.Intentions[1] >= secondObjective
                : IsLegacyQuestObjectiveReached(updatedQuest, requiredFirstObjective));
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(completionDialog).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 15 Hill Monster objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask TryApplyLegacyMap1000Family15FloorDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        string rateAttribute,
        string itemName,
        string dialog,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return;
        }

        if (!CheckLegacyMap1000Family15RewardRate(session.Character, rateAttribute, 30))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1000 family 15 Hill Monster floor drop " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate(30)=false\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(dialog).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

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
                        "WorldServer skipped legacy map 1000 family 15 Hill Monster floor drop " +
                        $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"AddGroundItem failed\" " +
                        $"branch=\"{sourceBranch}\".")
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
                        $"map1000-family15-{itemName.ToLowerInvariant().Replace(' ', '-')}-drop",
                        $"source {itemName} floor drop was sent but floor item runtime state is not loaded for pickup tracking",
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
                "WorldServer sent legacy map 1000 family 15 Hill Monster floor drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" floorUid={floorItem.Uid} x={floorItem.X} y={floorItem.Y} " +
                $"color={floorItem.PlusOrColor} ownerOnly={entry.OwnerOnly} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static bool CheckLegacyMap1000Family15RewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }
}
