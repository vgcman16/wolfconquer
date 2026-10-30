using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family13SnakemanBranchesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_020 ||
            monsterFamily.Id != 13)
        {
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family13ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_346,
                requiredKills: 40,
                completionMessage: "You killed 40 Snakeman.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1346 Snakeman objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family13ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_345,
                requiredKills: 20,
                completionMessage: "You killed 20 Snakeman.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1345 Snakeman objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family13ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_360,
                requiredKills: 60,
                completionMessage: "You killed 60 Snakeman.",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1360 Snakeman objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        await TryApplyLegacyMap1020Family13FloorDropAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_341,
                itemId: 720_857,
                rateAttribute: RuntimeLegacyMap1020Family13Quest1341RateAttribute,
                itemName: "Quest 1341 item",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1341 floor drop",
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyMap1020Family13CloudRidingScrollAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family13ObjectiveAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_335,
                requiredKills: null,
                completionMessage: null,
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1335 Snakeman objective",
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        await TryApplyLegacyMap1020Family13FloorDropAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                questId: 1_333,
                itemId: 720_854,
                rateAttribute: RuntimeLegacyMap1020Family13Quest1333RateAttribute,
                itemName: "Quest 1333 item",
                sourceBranch: "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1333 floor drop",
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyMap1020Family13Quest1753SmellAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);
        updatedContext = updatedContext with { Session = updatedSession };

        updatedSession = await TryApplyLegacyMap1020Family13Quest1783VenomousFangAsync(
                updatedContext,
                updatedSession,
                monster,
                monsterFamily,
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family13ObjectiveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint? requiredKills,
        string? completionMessage,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
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
        var completed = requiredKills is { } threshold &&
            updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var updatedQuest) &&
            IsLegacyQuestObjectiveReached(updatedQuest, threshold);
        if (completed && completionMessage is not null)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(completionMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 13 Snakeman objective " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} completed={completed} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask TryApplyLegacyMap1020Family13FloorDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint questId,
        uint itemId,
        string rateAttribute,
        string itemName,
        string sourceBranch,
        CancellationToken cancellationToken)
    {
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return;
        }

        if (!CheckLegacyMap1020Family13RewardRate(session.Character, rateAttribute, 20))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 13 Snakeman floor drop " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} itemName=\"{itemName}\" reason=\"Core.Rate(20)=false\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        var floorItem = LegacyFloorItemPacket.FromMonsterDrop(
            itemId,
            monster,
            session.Character,
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
                        "WorldServer skipped legacy map 1020 family 13 Snakeman floor drop " +
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
                        $"map1020-family13-quest{questId.ToString(CultureInfo.InvariantCulture)}-floor-drop",
                        $"source map 1020 family 13 quest {questId.ToString(CultureInfo.InvariantCulture)} floor drop was sent but floor item runtime state is not loaded for pickup tracking",
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

        await SendFloorItemVisibleAsync(context, session.Character, entry, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy map 1020 family 13 Snakeman floor drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemName=\"{itemName}\" floorUid={floorItem.Uid} x={floorItem.X} y={floorItem.Y} " +
                $"color={floorItem.PlusOrColor} ownerOnly={entry.OwnerOnly} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family13CloudRidingScrollAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_337;
        const uint itemId = 720_856;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1337 Cloud Riding Scroll";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Please make 1 more space in your inventory.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 13 Cloud Riding Scroll reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=\"inventory full\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) == 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer preserved legacy map 1020 family 13 Cloud Riding Scroll no-add branch " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=\"source Inventory.Contain returned false\" " +
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
                        "map1020-family13-cloud-riding-scroll",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants Cloud Riding Scroll for quest {questId.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
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

        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var currentSession) ||
            !ReferenceEquals(currentSession, session))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 13 Cloud Riding Scroll reward " +
                    $"{FormatSession(context)} reason=session-registration-missing-or-stale; no grant or receipt was committed.")
                .ConfigureAwait(false);
            return session;
        }

        var itemCountBefore = CountLegacyInventoryItem(session.PlayerItems, itemId);
        WorldSession updatedSession;
        byte[] receiptFrame;
        byte[] itemFrame;
        ushort itemMode;
        try
        {
            var updatedItems = AddLegacyInventoryItem(
                session.PlayerItems, itemType, stackCount: 1,
                out var rewardItem, out itemMode);
            updatedSession = session with { PlayerItems = updatedItems };
            receiptFrame = LegacyNpcReplyPacket
                .ForMessageBox("You~received~the~Cloud~Riding~Scroll!").ToFrameBytes();
            itemFrame = LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes();
        }
        catch (OverflowException)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 13 Cloud Riding Scroll reward " +
                    $"{FormatSession(context)} reason=reward-preparation-overflow; no grant or receipt was committed.")
                .ConfigureAwait(false);
            return session;
        }

        if (!sessions.TryCompareAndSet(context.Connection, session, updatedSession, out var final))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 13 Cloud Riding Scroll reward " +
                    $"{FormatSession(context)} reason=session-changed-before-commit; no grant or receipt was committed.")
                .ConfigureAwait(false);
            return session;
        }

        updatedSession = final!;

        // A synchronous real session observer may retire the committed owner.
        // This is a committed grant with suppressed publication, never rollback.
        if (!sessions.TryGet(context.Connection, out currentSession) ||
            !ReferenceEquals(currentSession, updatedSession))
            return updatedSession;
        await context.Connection.SendAsync(receiptFrame, cancellationToken).ConfigureAwait(false);
        if (!sessions.TryGet(context.Connection, out currentSession) ||
            !ReferenceEquals(currentSession, updatedSession))
            return updatedSession;
        await context.Connection.SendAsync(itemFrame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 13 Cloud Riding Scroll reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family13Quest1753SmellAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_753;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1753 Snake Man smell";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementsAsync(
                context,
                session,
                questId,
                [1, 0, 0],
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("You,ve collected the smell of Snake Man!").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 13 Snake Man smell objective " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1020Family13Quest1783VenomousFangAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_783;
        const uint itemId = 729_978;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1020 && Family.ID == 13 quest 1783 Venomous Fang";

        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!CheckLegacyMap1020Family13RewardRate(session.Character, RuntimeLegacyMap1020Family13Quest1783RateAttribute, 15))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1020 family 13 Snakeman quest 1783 Venomous Fang reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=\"Core.Rate(15)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map1020-family13-venomous-fang",
                        reason: $"source Inventory.Add({itemId.ToString(CultureInfo.InvariantCulture)}) grants Venomous Fang for quest {questId.ToString(CultureInfo.InvariantCulture)}, but the item type row was not loaded",
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
        var itemSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, itemSession);
        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem, mode: itemMode).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context,
                itemSession,
                questId,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyMap1002QuestAutoRouteGapAsync(
                context with { Session = updatedSession },
                monster,
                monsterFamily,
                questId,
                key: "map1020-family13-quest1783-autopatcher",
                message: "You,ve collected the smell of Snake Man!.",
                fallbackNpcId: 1_611,
                fallbackMapId: 1_002,
                fallbackX: 345,
                fallbackY: 251)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1020 family 13 Snakeman quest 1783 Venomous Fang reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} itemMode={itemMode} " +
                $"itemCountBefore={itemCountBefore} itemCountAfter={CountLegacyInventoryItem(updatedSession.PlayerItems, itemId)} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1020Family13RewardRate(
        CharacterSnapshot character,
        string attributeName,
        int ratePercent)
    {
        return TryReadAttributeBool(character.Attributes, out var forced, attributeName)
            ? forced
            : Random.Shared.Next(0, 100) < ratePercent;
    }
}
