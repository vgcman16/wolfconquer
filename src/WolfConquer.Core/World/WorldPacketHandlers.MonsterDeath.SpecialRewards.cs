using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacySpiritBeadQuest2375MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const uint questId = 2_375;
        const string sourceBranch = "MonsterRole.cs QuestGUI.CheckQuest(2375, Accepted) DailySpiritBeadCount";
        if (!session.Character.PlayerQuests.TryGetQuest(questId, out var quest) ||
            quest.Status != PlayerQuestStatus.Accepted)
        {
            return session;
        }

        var amount = GetLegacySpiritBeadQuest2375Amount(monster);
        var currentCount = ReadAttributeUInt64(session.Character.Attributes, "DailySpiritBeadCount");
        var updatedCount = checked(currentCount + amount);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            "DailySpiritBeadCount",
            updatedCount);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket
                    .SystemTalk($"You received {amount.ToString(CultureInfo.InvariantCulture)} spirites.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy quest 2375 spirit bead monster death credit " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"level={monster.Level} boss={monster.Boss} maxLife={monster.MaxLife} amount={amount} " +
                $"dailySpiritBeadCount={updatedCount} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static ulong GetLegacySpiritBeadQuest2375Amount(LegacyMonsterInstance monster)
    {
        if (monster.Level < 70)
        {
            return 1;
        }

        if (monster.Level <= 99)
        {
            return 2;
        }

        if (monster.Level <= 119)
        {
            return 3;
        }

        if (monster.Level < 140)
        {
            return 4;
        }

        return monster.Boss && monster.MaxLife >= 1_000_000
            ? 1_000UL
            : 0;
    }

    private static async ValueTask<WorldSession> TryApplyLegacyFamily4212Quest6126MonsterDeathAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monsterFamily.Id != 4_212)
        {
            return session;
        }

        const uint questId = 6_126;
        const string sourceBranch = "MonsterRole.cs Family.ID == 4212 quest 6126 team/killer objective";
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        var killerMapId = updatedSession.Character.MapId;
        var killerDynamicId = ReadCharacterDynamicId(updatedSession.Character);
        var teammateCredits = 0;

        if (context.TeamStates is { } teamStates &&
            context.Sessions is { } sessions)
        {
            foreach (var connection in teamStates.GetConnections(context.Connection))
            {
                if (ReferenceEquals(connection, context.Connection) ||
                    !sessions.TryGet(connection, out var teammateSession) ||
                    teammateSession is null ||
                    teammateSession.Character.MapId != killerMapId ||
                    ReadCharacterDynamicId(teammateSession.Character) != killerDynamicId ||
                    !teammateSession.Character.PlayerQuests.TryGetQuest(questId, out var teammateQuest) ||
                    teammateQuest.Status != PlayerQuestStatus.Accepted)
                {
                    continue;
                }

                await TryApplyLegacyQuestObjectiveIncrementAsync(
                        updatedContext with
                        {
                            Connection = connection,
                            Session = teammateSession,
                        },
                        teammateSession,
                        questId,
                        amount: 1,
                        sourceBranch,
                        cancellationToken)
                    .ConfigureAwait(false);
                teammateCredits++;
            }
        }

        var killerCredited = false;
        if (updatedSession.Character.PlayerQuests.TryGetQuest(questId, out var killerQuest) &&
            killerQuest.Status == PlayerQuestStatus.Accepted)
        {
            updatedSession = await TryApplyLegacyQuestObjectiveIncrementAsync(
                    updatedContext,
                    updatedSession,
                    questId,
                    amount: 1,
                    sourceBranch,
                    cancellationToken)
                .ConfigureAwait(false);
            updatedContext = updatedContext with { Session = updatedSession };
            killerCredited = true;
        }

        if (killerCredited || teammateCredits != 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer applied legacy family 4212 quest 6126 monster death credit " +
                    $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"map={monster.MapId} dynamicId={killerDynamicId} killerCredited={killerCredited} " +
                    $"teammateCredits={teammateCredits} branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
        }

        return updatedSession;
    }

}
