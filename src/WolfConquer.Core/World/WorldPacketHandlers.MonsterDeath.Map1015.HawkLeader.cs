using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyMap1015Family4431179HawkLeaderQuest1629Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_015 ||
            monsterFamily.Id != 4_431_179 ||
            !session.Character.PlayerQuests.TryGetQuest(1_629, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1015 && Family.ID == 4431179 quest 1629 HawkLeader objective";
        await SendLegacyMap1015Family19KillNoticeAsync(context, "You`ve killed a HawkLeader.", cancellationToken)
            .ConfigureAwait(false);
        var updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                context,
                session,
                1_629,
                amount: 1,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1015 family 4431179 quest 1629 HawkLeader objective " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"questId=1629 branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }
}
