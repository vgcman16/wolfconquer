using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40906Quest3646MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_906)
        {
            return session;
        }

        const uint questId = 3_646;
        const uint fragmentItemId = 3_008_754;
        const uint earthForceItemId = 3_008_745;
        const ushort requiredFragments = 5;
        const string autoRouteKey = "map3998-earth-force-autopatcher";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40906 quest 3646 Earth Force";
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
                    "WorldServer skipped legacy map 3998 Earth Force reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} fragmentItemId={fragmentItemId} earthForceItemId={earthForceItemId} " +
                    $"reason=inventory-full; no Earth Force was granted branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap3998Family40906EarthForceRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3998 Earth Force reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} reason=\"Core.Rate(40)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        if (CountLegacyInventoryItem(updatedSession.PlayerItems, earthForceItemId) >= 1)
        {
            await LogLegacyMap3998EarthForceAutoRouteGapAsync(
                    updatedContext,
                    monster,
                    monsterFamily,
                    questId,
                    earthForceItemId,
                    autoRouteKey)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3998 Earth Force auto-route check " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} earthForceItemId={earthForceItemId} addedFragment=False converted=False " +
                    $"alreadyHasEarthForce=True branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        var fragmentsBefore = CountLegacyInventoryItem(updatedSession.PlayerItems, fragmentItemId);
        var addedFragment = false;
        if (fragmentsBefore < requiredFragments)
        {
            if (!TryReadItemType(context.WorldData, fragmentItemId, out var fragmentItemType))
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            "itemtype.txt",
                            "map3998-earth-force-fragment",
                            reason: "source AddItemWitchStack(3008754) grants an Earth Force fragment for quest 3646, but the item type row was not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                                LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                                LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                                LegacyParityGapDiagnostics.Fact("itemId", fragmentItemId),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("itemId", fragmentItemId),
                            ]))
                    .ConfigureAwait(false);
                return session;
            }

            var updatedItems = AddOrStackLegacyInventoryItem(
                updatedSession.PlayerItems,
                fragmentItemType,
                fragmentItemId,
                stackCount: 1,
                out var fragmentItem,
                out var fragmentItemMode);
            updatedSession = updatedSession with { PlayerItems = updatedItems };
            context.Sessions?.Set(context.Connection, updatedSession);
            updatedContext = context with { Session = updatedSession };
            addedFragment = true;

            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(fragmentItem, mode: fragmentItemMode).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var fragmentsAfter = CountLegacyInventoryItem(updatedSession.PlayerItems, fragmentItemId);
        if (fragmentsAfter < requiredFragments)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3998 Earth Force fragment reward " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} fragmentItemId={fragmentItemId} fragmentsBefore={fragmentsBefore} " +
                    $"fragmentsAfter={fragmentsAfter} addedFragment={addedFragment} converted=False branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (!TryReadItemType(context.WorldData, earthForceItemId, out var earthForceItemType))
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3998-earth-force",
                        reason: "source Inventory.Add(3008745) grants Earth Force for quest 3646 after five fragments, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", earthForceItemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", earthForceItemId),
                        ]))
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (!TryConsumeLegacyInventoryItemCount(
                updatedSession.PlayerItems,
                fragmentItemId,
                requiredFragments,
                out var consumedItems,
                out var consumeFrames))
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "Inventory.Remove",
                        "map3998-earth-force-fragment-remove",
                        reason: "source Inventory.Remove(3008754, 5) converts five Earth Force fragments, but the modern inventory snapshot could not consume five loaded fragments",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", fragmentItemId),
                            LegacyParityGapDiagnostics.Fact("count", requiredFragments),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", fragmentItemId),
                        ]))
                .ConfigureAwait(false);
            return updatedSession;
        }

        foreach (var frame in consumeFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        var earthForceItems = AddLegacyInventoryItem(
            consumedItems,
            earthForceItemType,
            stackCount: 1,
            out var earthForceItem,
            out var earthForceItemMode);
        updatedSession = updatedSession with { PlayerItems = earthForceItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(earthForceItem, mode: earthForceItemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyMap3998EarthForceAutoRouteGapAsync(
                updatedContext,
                monster,
                monsterFamily,
                questId,
                earthForceItemId,
                autoRouteKey)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Earth Force reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} fragmentItemId={fragmentItemId} earthForceItemId={earthForceItemId} " +
                $"fragmentsBefore={fragmentsBefore} fragmentsAfter={fragmentsAfter} addedFragment={addedFragment} " +
                $"converted=True earthForceItemMode={earthForceItemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask LogLegacyMap3998EarthForceAutoRouteGapAsync(
        WorldPacketHandlerContext context,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        string key)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    key,
                    reason: "source sends QuestGUI.SendAutoPatcher after the player has Earth Force for quest 3646; modern runtime preserves the branch but the optional client auto-route prompt is not modeled yet",
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

    private static bool CheckLegacyMap3998Family40906EarthForceRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap3998Family40906EarthForceRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 40;
    }
}
