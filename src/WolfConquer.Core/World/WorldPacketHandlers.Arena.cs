using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyArenaMapId = 700;

    private const long LegacyArenaFirstPlayerX = 45;

    private const long LegacyArenaFirstPlayerY = 45;

    private const long LegacyArenaSecondPlayerX = 60;

    private const long LegacyArenaSecondPlayerY = 60;

    private const int LegacyArenaWatcherMinCoordinate = 35;

    private const int LegacyArenaWatcherMaxExclusiveCoordinate = 70;

    private const uint LegacyArenaServerId = 0;

    private const uint LegacyArenaRewardItemId = 723_912;

    private const uint LegacyArenaWinRewardThreshold = 9;

    private const uint LegacyArenaHeroRewardThreshold = 20;

    private const uint LegacyArenaHeroRewardGoalId = 506;

    private const uint LegacyArenaEndHeroRewardGoalId = 204;

    private const uint LegacyArenaBattleRewardThreshold = 20;

    private static readonly uint[] LegacyArenaWinnerActivenessTaskIds = [7, 19, 31, 36];


    [PacketHandler(WorldPacketOpcodes.LegacyArenaWatchers)]
    private static async ValueTask HandleLegacyArenaWatchersAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyArenaWatchersPacket.TryParseQuery(frame, out var query))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyArenaWatchers packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyArenaWatchers {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyArenaWatcherMode.RequestView &&
            TryFindActiveArenaMatchByFighterUid(context, query.Uid, out var arenaMatch))
        {
            await SendArenaWatcherRequestAsync(context, session, arenaMatch, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyArenaWatcherMode.RequestView &&
            TryFindActiveTeamArenaMatchByMemberUid(context, query.Uid, out var match))
        {
            await SendTeamArenaWatcherRequestAsync(context, session, match, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyArenaWatcherMode.QuitButton)
        {
            if (await HandleArenaWatcherQuitAsync(context, session, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await HandleTeamArenaWatcherQuitAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (query.Mode == LegacyArenaWatcherMode.Fighters)
        {
            if (TryFindActiveArenaMatchByFighterUid(context, query.Uid, out _))
            {
                await HandleArenaWatcherCheerAsync(context, query.Uid, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await HandleTeamArenaWatcherCheerAsync(context, query.Uid, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyArenaWatchers " +
                $"{FormatSession(context)} mode={query.Mode} uid={query.Uid}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask ApplyArenaMatchStatisticsAsync(
        WorldPacketHandlerContext context,
        ArenaCompletedMatch completed,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(completed.Winner, out var winnerSession) != true ||
            winnerSession is null ||
            context.Sessions?.TryGet(completed.Loser, out var loserSession) != true ||
            loserSession is null)
        {
            return;
        }

        var points = checked((uint)Random.Shared.Next(30, 50));
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        var winnerBefore = GetArenaStatisticsForSession(context, completed.Winner, winnerSession);
        var loserBefore = GetArenaStatisticsForSession(context, completed.Loser, loserSession);

        arenaStates.AddMatchWin(completed.Winner, points);
        arenaStates.AddMatchLoss(
            completed.Loser,
            points,
            subtractPoints: loserBefore.ArenaPoints > points);

        await ApplyArenaWinnerSideEffectsAsync(
                context,
                completed.Winner,
                winnerSession,
                winnerBefore,
                cancellationToken)
            .ConfigureAwait(false);
        await ApplyArenaBattleRewardAsync(
                context,
                completed.Loser,
                loserSession,
                loserBefore,
                cancellationToken)
            .ConfigureAwait(false);

        if (context.Sessions?.TryGet(completed.Winner, out var finalWinnerSession) == true &&
            finalWinnerSession is not null)
        {
            winnerSession = finalWinnerSession;
        }

        if (context.Sessions?.TryGet(completed.Loser, out var finalLoserSession) == true &&
            finalLoserSession is not null)
        {
            loserSession = finalLoserSession;
        }

        var winnerAfter = GetArenaStatisticsForSession(context, completed.Winner, winnerSession);
        await BroadcastArenaQualifierResultAsync(
                context,
                winnerSession,
                loserSession,
                winnerAfter.TodayRank,
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask BroadcastArenaQualifierResultAsync(
        WorldPacketHandlerContext context,
        WorldSession winnerSession,
        WorldSession loserSession,
        uint winnerTodayRank,
        CancellationToken cancellationToken)
    {
        var message = BuildArenaQualifierResultMessage(
            winnerSession.Character,
            loserSession.Character,
            winnerTodayRank);
        var messageFrame = LegacyMsgMessagePacket.SystemQualifier(message).ToFrameBytes();
        var activeSessions = context.Sessions?.Snapshot();
        if (activeSessions is { Count: > 0 })
        {
            foreach (var active in activeSessions)
            {
                await active.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await context.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer broadcast Arena Qualifier result " +
                $"winner={winnerSession.Character.Name} loser={loserSession.Character.Name} todayRank={winnerTodayRank}.")
            .ConfigureAwait(false);
    }


    private static string BuildArenaQualifierResultMessage(
        CharacterSnapshot winner,
        CharacterSnapshot loser,
        uint winnerTodayRank)
    {
        var builder = new StringBuilder();
        AppendArenaQualifierName(builder, winner);
        builder.Append(" has defeated ");
        AppendArenaQualifierName(builder, loser);
        if (winnerTodayRank != 0)
        {
            builder.Append(" in the Qualifier, and is currently ranked No. ");
            builder.Append(winnerTodayRank);
        }
        else
        {
            builder.Append(" in the Qualifier");
        }

        builder.Append('.');
        return builder.ToString();
    }


    private static void AppendArenaQualifierName(StringBuilder builder, CharacterSnapshot character)
    {
        var guildName = ReadAttributeString(character.Attributes, "GuildName");
        if (!string.IsNullOrWhiteSpace(guildName))
        {
            builder.Append('(');
            builder.Append(guildName);
            builder.Append(") ");
        }

        builder.Append(character.Name);
    }


    private static async ValueTask ApplyArenaWinnerSideEffectsAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        ArenaStatistics before,
        CancellationToken cancellationToken)
    {
        var updatedSession = session;

        if (before.TodayWin == LegacyArenaWinRewardThreshold)
        {
            updatedSession = await GrantArenaRewardItemAsync(
                    context,
                    connection,
                    updatedSession,
                    "today-win threshold",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (before.TodayWin == LegacyArenaHeroRewardThreshold)
        {
            updatedSession = AddLegacyHeroRewardGoal(updatedSession, LegacyArenaHeroRewardGoalId);
            context.Sessions?.Set(connection, updatedSession);
        }

        foreach (var taskId in LegacyArenaWinnerActivenessTaskIds)
        {
            updatedSession = await IncreaseLegacyActivityTaskAsync(
                    context,
                    connection,
                    updatedSession,
                    taskId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await ApplyArenaBattleRewardAsync(context, connection, updatedSession, before, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask ApplyArenaBattleRewardAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        ArenaStatistics before,
        CancellationToken cancellationToken)
    {
        var after = GetArenaStatisticsForSession(context, connection, session);
        if (after.TodayBattles != LegacyArenaBattleRewardThreshold)
        {
            return;
        }

        await GrantArenaRewardItemAsync(
                context,
                connection,
                session,
                "today-battle threshold",
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask<WorldSession> GrantArenaRewardItemAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        string reason,
        CancellationToken cancellationToken)
    {
        return await GrantLegacyQualifierRewardItemAsync(
                context,
                connection,
                session,
                LegacyArenaRewardItemId,
                "Arena",
                reason,
                cancellationToken)
            .ConfigureAwait(false);
    }

}
