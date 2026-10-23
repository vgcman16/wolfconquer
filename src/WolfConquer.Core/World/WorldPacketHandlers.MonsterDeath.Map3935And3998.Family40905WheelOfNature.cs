using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap3998Family40905Quest3645MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 3_998 ||
            monsterFamily.Id != 40_905)
        {
            return session;
        }

        const uint questId = 3_645;
        const uint itemId = 3_008_743;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 3998 && Family.ID == 40905 quest 3645 Wheel of Nature";
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
                    "WorldServer skipped legacy map 3998 Wheel of Nature reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=inventory-full branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        if (!CheckLegacyMap3998Family40905WheelOfNatureRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 3998 Wheel of Nature reward " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} reason=\"Core.Rate(40)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return session;
        }

        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        if (CountLegacyInventoryItem(updatedSession.PlayerItems, itemId) >= 1)
        {
            await LogParityGapAsync(
                    updatedContext,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "QuestGUI.SendAutoPatcher",
                        "map3998-wheel-of-nature-autopatcher",
                        reason: "source sends QuestGUI.SendAutoPatcher after the player already has the Wheel of Nature for quest 3645; modern runtime preserves the already-owned branch but the optional client auto-route prompt is not modeled yet",
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
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3998 Wheel of Nature auto-route check " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} itemId={itemId} addedItem=False branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        if (!TryReadItemType(context.WorldData, itemId, out var itemType))
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        "itemtype.txt",
                        "map3998-wheel-of-nature",
                        reason: "source AddItemWitchStack(3008743) grants the Wheel of Nature for quest 3645, but the item type row was not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                            LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                            LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("itemId", itemId),
                        ]))
                .ConfigureAwait(false);
            return session;
        }

        if (!TryCommitLegacyNatureForceItem(context, session, itemType, itemId, stackReward: true,
                out var prepared, out var reason))
        {
            await LogLegacyNatureForceRefusalAsync(context, monsterFamily.Id, reason).ConfigureAwait(false);
            return session;
        }
        var rewardItem = prepared.Item;
        var itemMode = prepared.Mode;
        updatedSession = prepared.Session;
        updatedContext = context with { Session = updatedSession };
        if (!IsLegacyNatureForceSessionCurrent(context, updatedSession)) return updatedSession;
        await context.Connection.SendAsync(prepared.Frame, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Wheel of Nature reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} itemId={itemId} stack={rewardItem.StackSize} itemMode={itemMode} " +
                $"addedItem=True branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool CheckLegacyMap3998Family40905WheelOfNatureRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap3998Family40905WheelOfNatureRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 40;
    }
}
