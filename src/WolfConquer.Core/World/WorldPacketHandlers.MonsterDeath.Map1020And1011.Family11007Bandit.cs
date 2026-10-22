using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1011Family11007BanditQuest1103BranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_011 ||
            monsterFamily.Id != 11_007)
        {
            return session;
        }

        const uint questId = 1_103;
        const uint itemId = 721_263;
        const uint sourceDurabilityItemId = 729_094;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1011 && Family.ID == 11007 Bandit drop and quest 1103";

        await TryApplyLegacyMap1011Family11007BanditFloorDropAsync(
                context,
                session,
                monster,
                monsterFamily,
                itemId,
                sourceDurabilityItemId,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);

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
            IsLegacyQuestObjectiveReached(updatedQuest, 10);
        if (completed)
        {
            await LogLegacyMap1011Family11007BanditQuest1103AutoRouteGapAsync(
                    updatedContext,
                    monster,
                    monsterFamily)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1011 family 11007 Bandit quest objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask TryApplyLegacyMap1011Family11007BanditFloorDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint itemId,
        uint sourceDurabilityItemId,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!CheckLegacyMap1011Family11007BanditDropRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1011 family 11007 Bandit floor drop " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} reason=\"Core.Rate(4)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        PlayerItemSnapshotItem? pickupItem = null;
        if (TryReadItemType(context.WorldData, sourceDurabilityItemId, out var sourceItemType))
        {
            var durability = checked((ushort)sourceItemType.LegacyDurability);
            pickupItem = new PlayerItemSnapshotItem
            {
                Uid = GetNextSessionItemUid(session.PlayerItems),
                ItemId = itemId,
                Durability = durability,
                MaximumDurability = durability,
                Position = 0,
                StackSize = 1,
            };
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1011-family11007-bandit-source-durability-item",
                        reason: $"source family 11007 Bandit item {itemId.ToString(CultureInfo.InvariantCulture)} copies durability from item {sourceDurabilityItemId.ToString(CultureInfo.InvariantCulture)}, but that item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                            LegacyParityGapDiagnostics.Fact("sourceDurabilityItemId", sourceDurabilityItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
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
        var entry = FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, pickupItem, ownerOnly: true, dynamicId: monster.DynamicId);
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
                        "WorldServer skipped legacy map 1011 family 11007 Bandit floor drop " +
                        $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"itemId={itemId} reason=\"AddGroundItem failed\" branch=\"{sourceBranch}\".")
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
                        "map1011-family11007-bandit-floor-drop",
                        "source map 1011 family 11007 Bandit floor drop was sent but floor item runtime state is not loaded for pickup tracking",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, session.Character, entry, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy map 1011 family 11007 Bandit floor drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"itemId={itemId} sourceDurabilityItemId={sourceDurabilityItemId} " +
                $"floorUid={floorItem.Uid} x={floorItem.X} y={floorItem.Y} " +
                $"color={floorItem.PlusOrColor} ownerOnly={entry.OwnerOnly} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static bool CheckLegacyMap1011Family11007BanditDropRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, RuntimeLegacyMap1011Family11007BanditDropRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 4;
    }

    private static async ValueTask LogLegacyMap1011Family11007BanditQuest1103AutoRouteGapAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        const uint questId = 1_103;
        const string message = "You`ve~killed~10~Bandits.~Go~tell~Yu~Jing~about~it.";
        const uint fallbackNpcId = 8_267;
        var finishAnchor = TryGetLegacyQuestMission(context.WorldData, questId, out var mission)
            ? mission.FinishNpcAnchors.FirstOrDefault()
            : null;
        var finishNpcId = checked((uint)(finishAnchor?.NpcId ?? (int)fallbackNpcId));
        var finishMapId = checked((uint)(finishAnchor?.MapId ?? 0));
        var finishX = checked((uint)(finishAnchor?.X ?? 0));
        var finishY = checked((uint)(finishAnchor?.Y ?? 0));

        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    "map1011-family11007-quest1103-autopatcher",
                    reason: $"source sends QuestGUI.SendAutoPatcher(\"{message}\") after GetFinishQuest(8267, Player.Class, 1103); modern runtime updates quest 1103, but the optional client auto-route prompt packet is not modeled yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("message", message),
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
