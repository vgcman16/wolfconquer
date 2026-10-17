using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1001BullGhostAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 1_837;
        const uint itemId = 710_261;
        const ushort effectX = 102;
        const ushort effectY = 292;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1001 && Family.ID == 2231 && Quest 1837 Accepted";

        if (monsterFamily.Id != 2_231 ||
            !session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var distance = LegacyGetDistance(session.Character.X, session.Character.Y, effectX, effectY);
        if (distance > 3)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("Only when you herd the Mad Bull to (102,292) before killing them can you capture its ghost.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Bull Ghost capture " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"distance={distance} reason=too-far branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        await SendLegacyLocationEffectToVisiblePlayersAsync(
                context,
                session,
                effectX,
                effectY,
                "zf2-e027",
                cancellationToken)
            .ConfigureAwait(false);

        if (CountLegacyInventoryItem(session.PlayerItems, itemId) >= 3)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You`ve collected 3 Bull Ghost.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1001 Bull Ghost collected notice " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} count=3 branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!HasLegacyInventorySpace(session.PlayerItems, 1))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("You inventory is full. Make some room and come back for the Bull Ghost.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1001 Bull Ghost capture " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap1001BullGhostCaptureRate(session.Character))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket
                        .SystemTalk("Well, pay more attention next time. You failed in retrieving the Bull Ghost.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 1001 Bull Ghost failed capture " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"itemId={itemId} branch=\"{sourceBranch}; Core.Rate(1, 3)=false\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map-1001-bull-ghost",
                        reason: "source grants Bull Ghost item 710261 on monster death, but the item type was not loaded",
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
                LegacyMsgMessagePacket.SystemTalk("You obtained a Bull Ghost.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var updatedCount = CountLegacyInventoryItem(updatedItems, itemId);
        if (updatedCount >= 3)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You`ve collected 3 Bull Ghost.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1001 Bull Ghost capture " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"familyId={monsterFamily.Id} itemId={itemId} mode={itemMode} count={updatedCount} " +
                $"branch=\"{sourceBranch}; Core.Rate(1, 3)=true\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap1001BullGhostCaptureRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap1001BullGhostCaptureRateAttribute)
            ? forced
            : Random.Shared.Next(0, 3) == 0;
    }
}
