using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1001BullMasterHeartAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_838;
        const uint itemId = 710_260;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1001 && Family.ID == 2232 && Quest 1838 Accepted";

        if (monsterFamily.Id != 2_232 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox("You~received~the~Bull~heart!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1001 Bull Master heart notice " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} reason=already-owned branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("Your~inventory~is~full.~You`ll~need~to~make~some~room,~first.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Bull Master heart reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map-1001-bull-master-heart",
                        reason: "source grants Bull heart item 710260 on monster death, but the item type was not loaded",
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

        var updatedItems = AddLegacyInventoryItem(
            session.PlayerItems,
            itemType,
            stackCount: 1,
            out var rewardItem,
            out var itemMode);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

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
                LegacyNpcReplyPacket
                    .ForMessageBox("You~killed~the~Bull~Master~and~received~its~heart.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1001 Bull Master heart reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} itemId={itemId} mode={itemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1001BurntBullHornAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_836;
        const uint itemId = 710_259;
        const string completedMessage =
            "You`ve obtained 10 Burnt Bull Horns and put it in your inventory.";
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1001 && Family.ID == 57 && Quest 1836 Accepted";

        if (monsterFamily.Id != 57 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        if (!HasLegacyMap1001FerventPill(session.Character))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("Bring the Fervent Pill received from Master Mo Mo to go to the Mystic Castle. Use the pill first, and then kill the Bull Monsters to get 10 Burnt Bull Horns for Master Mo Mo.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Burnt Bull Horn reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} reason=fervent-pill-inactive branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 10)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(completedMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1001 Burnt Bull Horn collected notice " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} count=10 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("Your inventory is full. Make some room and come back for the Burnt Bull Horn.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Burnt Bull Horn reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map-1001-burnt-bull-horn",
                        reason: "source grants Burnt Bull Horn item 710259 on monster death, but the item type was not loaded",
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
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        rewardItem,
                        LegacyGameItemPacket.CreateRuneSpellOverride(rewardItem, context.WorldData?.ItemsById),
                        itemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedCount = CountLegacyInventoryItem(updatedItems, itemId);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket
                    .SystemTalk(updatedCount >= 10 ? completedMessage : "You`ve obtained a Burnt Bull Horn.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1001 Burnt Bull Horn reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} itemId={itemId} mode={itemMode} count={updatedCount} " +
                $"branch=\"{sourceBranch}; OnFerentPill=true\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMap1001JinxTombBatHeartAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint itemId = 711_518;
        const string sourceBranch = "MonsterRole.cs killer.Player.Map == 1001 && Family.ID == 2189";

        if (monsterFamily.Id != 2_189 ||
            CountLegacyInventoryItem(session.PlayerItems, itemId) >= 1)
        {
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox("Your~inventory~is~full.~Hurry~to~make~1~empty~space.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Jinx Tomb Bat heart reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map-1001-jinx-tomb-bat-heart",
                        reason: "source grants Jinx Tomb Bat Heart item 711518 on monster death, but the item type was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
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
                LegacyNpcReplyPacket
                    .ForMessageBox("You~killed~the~Jinx~Tomb~Bat~and~received~a~Jinx~Tomb~Bat~Heart!")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1001 Jinx Tomb Bat heart reward " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} itemId={itemId} mode={itemMode} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

}
