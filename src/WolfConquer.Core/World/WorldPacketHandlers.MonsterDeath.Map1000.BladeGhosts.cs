using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family2163Quest525HandkerchiefAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 525;
        const uint sourceItemId = 721_777;
        const uint rewardItemId = 721_782;
        const string sourceDialog =
            "Your~gut~feeling~told~you~that~the~Bud~Handkerchief~has~something~to~do~with~Yang~Yun~in~Bird~Island.~Find~him~(732,516)~now.";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 2163 quest 525 Bud Handkerchief conversion";

        if (monster.MapId != 1_000 ||
            monsterFamily.Id != 2_163 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
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
                    "WorldServer skipped legacy map 1000 family 2163 quest 525 handkerchief conversion " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} sourceItemId={sourceItemId} rewardItemId={rewardItemId} " +
                    $"reason=\"inventory full\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var alreadyHasReward = CountLegacyInventoryItem(session.PlayerItems, rewardItemId) >= 1;
        if (alreadyHasReward)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(sourceDialog).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1000 family 2163 quest 525 handkerchief hint " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} sourceItemId={sourceItemId} rewardItemId={rewardItemId} " +
                    $"alreadyHasBudVillagerHandkerchief=True branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, rewardItemId, out var rewardItemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1000-family2163-bud-villager-handkerchief",
                        reason: "source Inventory.Add(721782) grants Bud Villager Handkerchief for quest 525, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", rewardItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", rewardItemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        var convertedItems = session.PlayerItems;
        var consumedSource = TryConsumeLegacyInventoryItemCount(
            session.PlayerItems,
            sourceItemId,
            count: 1,
            out var consumedItems,
            out var consumeFrames);
        if (consumedSource)
        {
            convertedItems = consumedItems;
            foreach (var frame in consumeFrames)
            {
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "Inventory.Remove",
                        "map1000-family2163-bud-handkerchief-remove",
                        reason: "source Inventory.Remove(721777, 1) converts Bud Handkerchief, but the modern inventory snapshot could not consume one loaded source item",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", sourceItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("questId", questId),
                            LegacyParityGapDiagnostics.Fact("itemId", sourceItemId),
                        ]))
                .ConfigureAwait(false);
        }

        var updatedItems = AddLegacyInventoryItem(
            convertedItems,
            rewardItemType,
            stackCount: 1,
            out var rewardItem,
            out var rewardItemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: rewardItemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(sourceDialog).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 2163 quest 525 handkerchief conversion " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} sourceItemId={sourceItemId} rewardItemId={rewardItemId} " +
                $"consumedSource={consumedSource} itemMode={rewardItemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family2179Quest525CapriceLeaderSpawnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 525;
        const uint rewardItemId = 721_782;
        const int spawnedMonsterId = 2_163;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 2179 quest 525 Caprice Leader spawn";

        if (monster.MapId != 1_000 ||
            monsterFamily.Id != 2_179 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1000Family2179CapriceLeaderRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1000 family 2179 quest 525 Caprice Leader spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} spawnedMonsterId={spawnedMonsterId} reason=\"Core.Rate(20)=false\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, rewardItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1000 family 2179 quest 525 Caprice Leader spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={rewardItemId} spawnedMonsterId={spawnedMonsterId} " +
                    $"reason=\"Bud Villager Handkerchief already in inventory\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("Caprice~Leader~has~appeared~at~(170,230).").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var dynamicId = ReadCharacterDynamicId(session.Character);
        var spawned = await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                context,
                session,
                monster,
                monsterFamily,
                spawnedMonsterId,
                mapId: 1_000,
                x: 170,
                y: 230,
                dynamicId: checked((long)dynamicId),
                sourceBranch: "MonsterRole.cs Map.AddMapMonster monsterId=2163 ride_screen",
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 2179 quest 525 Caprice Leader spawn " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} spawnedMonsterId={spawnedMonsterId} spawned={spawned} " +
                $"dynamicId={dynamicId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return session;
    }

    private static bool CheckLegacyMap1000Family2179CapriceLeaderRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, RuntimeLegacyMap1000Family2179CapriceLeaderRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 20;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1000Family55434417Quest1498AngryBladeGhostAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_498;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1000 && Family.ID == 55434417 quest 1498 AngryBladeGhost objective";

        if (monster.MapId != 1_000 ||
            monsterFamily.Id != 55_434_417 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You`ve killed a AngryBladeGhost.").ToFrameBytes(),
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
            IsLegacyQuestObjectiveReached(updatedQuest, 3);
        if (completed)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("Sculptor~He~has~finished~the~inscription!~Go~visit~him,~right~away.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1000 family 55434417 quest 1498 AngryBladeGhost objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
