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

        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var current) ||
            !ReferenceEquals(current, session))
        {
            await LogLegacyEarthForcePreparationRefusalAsync(context, "session-registration-missing-or-stale")
                .ConfigureAwait(false);
            return session;
        }

        if (!TryPrepareLegacyEarthForceReward(context, session, out var reward, out var reason))
        {
            await LogLegacyEarthForcePreparationRefusalAsync(context, reason).ConfigureAwait(false);
            return session;
        }

        updatedSession = session with { PlayerItems = reward.Items };
        if (!sessions.TryCompareAndSet(context.Connection, session, updatedSession))
        {
            await LogLegacyEarthForcePreparationRefusalAsync(context, "session-changed-before-commit")
                .ConfigureAwait(false);
            return session;
        }
        updatedContext = context with { Session = updatedSession };

        // The full state and all native frames are prepared before this single
        // commit. Observer retirement suppresses stale output, not the grant.
        foreach (var frame in reward.Frames)
        {
            if (!sessions.TryGet(context.Connection, out current) ||
                !ReferenceEquals(current, updatedSession))
                return updatedSession;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        if (!reward.Converted)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy map 3998 Earth Force fragment reward " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"questId={questId} fragmentItemId={fragmentItemId} fragmentsBefore={reward.FragmentsBefore} " +
                    $"fragmentsAfter={reward.FragmentsAfter} addedFragment={reward.AddedFragment} converted=False branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return updatedSession;
        }

        await LogLegacyMap3998EarthForceAutoRouteGapAsync(
                updatedContext, monster, monsterFamily, questId, earthForceItemId, autoRouteKey)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 3998 Earth Force reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId={questId} fragmentItemId={fragmentItemId} earthForceItemId={earthForceItemId} " +
                $"fragmentsBefore={reward.FragmentsBefore} fragmentsAfter={reward.FragmentsAfter} addedFragment={reward.AddedFragment} " +
                $"converted=True earthForceItemMode={reward.EarthForceItemMode} branch=\"{sourceBranch}\".")
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
