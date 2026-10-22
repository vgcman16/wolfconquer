using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family11223VultureBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint quest1306Id = 1_306;
        const uint quest1301Id = 1_301;
        const uint quest1736Id = 1_736;
        const uint featherItemId = 729_976;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 11223 Vulture branches";

        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 11_223)
        {
            return session;
        }

        var updatedSession = session;
        var updatedContext = context;
        if (updatedSession.Character.PlayerQuests.TryGetQuest(quest1306Id, out var quest1306) &&
            quest1306.Status == PlayerQuestStatus.Accepted)
        {
            if (HasLegacyInventorySpace(updatedSession.PlayerItems, 1))
            {
                if (TryReadItemType(context.WorldData, featherItemId, out var itemType))
                {
                    var itemCountBefore = CountLegacyInventoryItem(updatedSession.PlayerItems, featherItemId);
                    var updatedItems = AddLegacyInventoryItem(
                        updatedSession.PlayerItems,
                        itemType,
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
                    await context.Connection.SendAsync(
                            LegacyNpcReplyPacket
                                .ForMessageBox("You~received~a~Vulture~Feather.~Climb~up~to~the~City~Wall~at~(554,606)~to~burn~it!")
                                .ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            "WorldServer applied legacy map 1020 family 11223 quest 1306 Vulture Feather reward " +
                            $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                            $"questId={quest1306Id} itemId={featherItemId} itemMode={itemMode} " +
                            $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, featherItemId)} " +
                            $"branch=\"{sourceBranch}\".")
                        .ConfigureAwait(false);
                }
                else
                {
                    await LogParityGapAsync(
                            context,
                            LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                                "itemtype.txt",
                                "map1020-family11223-quest1306-vulture-feather",
                                reason: $"source Inventory.Add({featherItemId.ToString(CultureInfo.InvariantCulture)}) grants Vulture Feather for quest {quest1306Id.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
                                facts:
                                [
                                    LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                    LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                    LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                    LegacyParityGapDiagnostics.Fact("questId", quest1306Id),
                                    LegacyParityGapDiagnostics.Fact("itemId", featherItemId),
                                ],
                                dedupeFacts:
                                [
                                    LegacyParityGapDiagnostics.Fact("questId", quest1306Id),
                                    LegacyParityGapDiagnostics.Fact("itemId", featherItemId),
                                ]))
                        .ConfigureAwait(false);
                }
            }
            else
            {
                await context.Connection.SendAsync(
                        LegacyNpcReplyPacket.ForMessageBox("Please make 1 more space in your inventory.").ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer skipped legacy map 1020 family 11223 quest 1306 Vulture Feather reward " +
                        $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"questId={quest1306Id} itemId={featherItemId} reason=\"inventory full\" branch=\"{sourceBranch}\".")
                    .ConfigureAwait(false);
            }
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(quest1301Id, out var quest1301) &&
            quest1301.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    quest1301Id,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = context with { Session = updatedSession };
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1020 family 11223 quest 1301 objective " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={quest1301Id} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        if (!CheckLegacyMap1020Family11223VultureFeatherRate(updatedSession.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 11223 Vulture Feather floor drop " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={featherItemId} reason=\"Core.Rate(15)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (updatedSession.Character.PlayerQuests.TryGetQuest(quest1736Id, out var quest1736) &&
            quest1736.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                    updatedContext,
                    updatedSession,
                    quest1736Id,
                    [0, 1],
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = context with { Session = updatedSession };
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("You`ve~found~a~Vulture~Feather.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
            featherItemId,
            monster,
            updatedSession.Character,
            NextLegacyFloorItemUid()) with
        {
            PlusOrColor = 14,
        };
        var entry = FloorItemRuntimeEntry.ForItem(monster.MapId, floorItem, ownerOnly: true, dynamicId: monster.DynamicId);
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
                        "WorldServer skipped legacy map 1020 family 11223 Vulture Feather floor drop " +
                        $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                        $"itemId={featherItemId} reason=\"AddGroundItem failed\" branch=\"{sourceBranch}\".")
                    .ConfigureAwait(false);
                return updatedSession;
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
                        "map1020-family11223-vulture-feather-floor-drop",
                        "source map 1020 family 11223 Vulture Feather floor drop was sent but floor item runtime state is not loaded for pickup tracking",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", featherItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", featherItemId),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, updatedSession.Character, entry, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy map 1020 family 11223 Vulture Feather floor drop " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"itemId={featherItemId} floorUid={floorItem.Uid} x={floorItem.X} y={floorItem.Y} " +
                $"color={floorItem.PlusOrColor} ownerOnly={entry.OwnerOnly} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1020Family11223VultureFeatherRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, RuntimeLegacyMap1020Family11223VultureFeatherRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 15;
    }
}
