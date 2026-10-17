using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyTeamArenaMapId = LegacyArenaMapId;

    private const long LegacyTeamArenaFirstTeamX = 45;

    private const long LegacyTeamArenaFirstTeamY = 45;

    private const long LegacyTeamArenaSecondTeamX = 60;

    private const long LegacyTeamArenaSecondTeamY = 60;

    private const long LegacyTeamArenaWatcherX = 52;

    private const long LegacyTeamArenaWatcherY = 52;

    private const uint LegacyTeamArenaRewardItemId = 723_912;

    private const uint LegacyTeamArenaWinRewardThreshold = 9;

    private const uint LegacyTeamArenaHeroRewardThreshold = 20;

    private const uint LegacyTeamArenaHeroRewardGoalId = 507;

    private const uint LegacyTeamArenaBattleRewardThreshold = 20;

    private static readonly uint[] LegacyTeamArenaWinnerActivenessTaskIds = [12, 24];


    private static async ValueTask ApplyTeamArenaMatchStatisticsAsync(
        WorldPacketHandlerContext context,
        TeamArenaCompletedMatch completed,
        CancellationToken cancellationToken)
    {
        var points = checked((uint)Random.Shared.Next(30, 50));
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        foreach (var connection in teamStates.GetConnections(completed.Winner))
        {
            if (context.Sessions?.TryGet(connection, out var session) == true && session is not null)
            {
                var before = GetTeamArenaStatisticsForSession(context, connection, session);
                teamArenaStates.AddMatchWin(connection, points);
                await ApplyTeamArenaWinnerSideEffectsAsync(context, connection, session, before, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            teamArenaStates.AddMatchWin(connection, points);
        }

        foreach (var connection in teamStates.GetConnections(completed.Loser))
        {
            if (context.Sessions?.TryGet(connection, out var session) == true && session is not null)
            {
                var before = GetTeamArenaStatisticsForSession(context, connection, session);
                teamArenaStates.AddMatchLoss(connection, points);
                await ApplyTeamArenaBattleRewardAsync(context, connection, session, before, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            teamArenaStates.AddMatchLoss(connection, points);
        }
    }


    private static async ValueTask ApplyTeamArenaWinnerSideEffectsAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        TeamArenaStatistics before,
        CancellationToken cancellationToken)
    {
        var after = GetTeamArenaStatisticsForSession(context, connection, session);
        var updatedSession = session;

        if (before.TodayWin < LegacyTeamArenaWinRewardThreshold &&
            after.TodayWin == LegacyTeamArenaWinRewardThreshold)
        {
            updatedSession = await GrantTeamArenaRewardItemAsync(
                    context,
                    connection,
                    updatedSession,
                    "today-win threshold",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (before.TodayWin < LegacyTeamArenaHeroRewardThreshold &&
            after.TodayWin == LegacyTeamArenaHeroRewardThreshold)
        {
            updatedSession = AddLegacyHeroRewardGoal(updatedSession, LegacyTeamArenaHeroRewardGoalId);
            context.Sessions?.Set(connection, updatedSession);
        }

        foreach (var taskId in LegacyTeamArenaWinnerActivenessTaskIds)
        {
            updatedSession = await IncreaseLegacyActivityTaskAsync(
                    context,
                    connection,
                    updatedSession,
                    taskId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await ApplyTeamArenaBattleRewardAsync(context, connection, updatedSession, before, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask ApplyTeamArenaBattleRewardAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        TeamArenaStatistics before,
        CancellationToken cancellationToken)
    {
        var after = GetTeamArenaStatisticsForSession(context, connection, session);
        if (before.TodayBattles >= LegacyTeamArenaBattleRewardThreshold ||
            after.TodayBattles != LegacyTeamArenaBattleRewardThreshold)
        {
            return;
        }

        await GrantTeamArenaRewardItemAsync(
                context,
                connection,
                session,
                "today-battle threshold",
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask<WorldSession> GrantTeamArenaRewardItemAsync(
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
                LegacyTeamArenaRewardItemId,
                "TeamArena",
                reason,
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask ExportTeamArenaCompletedWatchersAsync(
        WorldPacketHandlerContext context,
        TeamArenaCompletedMatch completed,
        CancellationToken cancellationToken)
    {
        if (completed.Watchers is null || completed.Watchers.Count == 0)
        {
            return;
        }

        var remaining = new List<ConquerTcpConnection>(completed.Watchers);
        foreach (var watcher in completed.Watchers)
        {
            remaining.Remove(watcher);
            var update = new TeamArenaWatcherUpdate(
                completed.Match,
                remaining.ToArray(),
                completed.FirstCheers,
                completed.SecondCheers);
            WorldSession? watcherSession = null;
            if (context.Sessions?.TryGet(watcher, out var foundWatcherSession) == true &&
                foundWatcherSession is not null)
            {
                watcherSession = foundWatcherSession;
            }

            if (watcherSession is not null)
            {
                await watcher.SendAsync(
                        CreateTeamArenaWatchersFrame(context, LegacyArenaWatcherMode.Leave, update),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var watchers = CreateTeamArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);
            await SendTeamArenaFrameToWatchersAsync(context, update, watchers, cancellationToken)
                .ConfigureAwait(false);
            await SendTeamArenaFrameToTeamAsync(context, completed.Match.First, watchers, cancellationToken)
                .ConfigureAwait(false);
            await SendTeamArenaFrameToTeamAsync(context, completed.Match.Second, watchers, cancellationToken)
                .ConfigureAwait(false);
            await SendTeamArenaCompletedMatchScoreAsync(context, completed, update.Watchers, cancellationToken)
                .ConfigureAwait(false);

            if (watcherSession is null)
            {
                continue;
            }

            await watcher.SendAsync(
                    LegacyArenaWatchersPacket.Create(
                        LegacyArenaWatcherMode.Leave,
                        dwParam: 0,
                        uid: 0,
                        count: 0,
                        player1Cheers: 0,
                        player2Cheers: 0,
                        entries: []).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await ReturnTeamArenaWatcherAsync(context, watcher, watcherSession, cancellationToken)
                .ConfigureAwait(false);
        }
    }


    private static async ValueTask SendTeamArenaCompletedMatchScoreAsync(
        WorldPacketHandlerContext context,
        TeamArenaCompletedMatch completed,
        IReadOnlyList<ConquerTcpConnection> watchers,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeamLeaderSession(context, completed.Match.First, out var firstLeader) ||
            !TryGetTeamLeaderSession(context, completed.Match.Second, out var secondLeader))
        {
            return;
        }

        var firstStatistics = GetTeamArenaStatisticsForSession(context, firstLeader.Connection, firstLeader.Session);
        var secondStatistics = GetTeamArenaStatisticsForSession(context, secondLeader.Connection, secondLeader.Session);
        var frame = LegacyTeamArenaMatchScorePacket.Create(
                CheckedUInt32(firstLeader.Session.Character.Uid, nameof(firstLeader.Session.Character.Uid)),
                firstStatistics.TodayRank,
                firstLeader.Session.Character.Name,
                completed.FirstDamage,
                CheckedUInt32(secondLeader.Session.Character.Uid, nameof(secondLeader.Session.Character.Uid)),
                secondStatistics.TodayRank,
                secondLeader.Session.Character.Name,
                completed.SecondDamage)
            .ToFrameBytes();

        await SendTeamArenaFrameToTeamAsync(context, completed.Match.First, frame, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFrameToTeamAsync(context, completed.Match.Second, frame, cancellationToken)
            .ConfigureAwait(false);
        foreach (var watcher in watchers)
        {
            await watcher.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static async ValueTask SendTeamArenaDialogAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        WorldSession leaderSession,
        TeamArenaStatistics statistics,
        LegacyTeamArenaSignupDialogButton result,
        CancellationToken cancellationToken)
    {
        var frame = LegacyTeamArenaSignupResponsePacket.FromRequest(
                new LegacyTeamArenaSignupPacket(
                    LegacyTeamArenaSignupDialogType.Dialog,
                    result),
                leaderSession.Character,
                statistics)
            .ToFrameBytes();

        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(teamConnection))
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static async ValueTask SendTeamArenaFinalDialogAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        WorldSession leaderSession,
        LegacyTeamArenaSignupDialogButton result,
        CancellationToken cancellationToken)
    {
        var leaderStatistics = GetTeamArenaStatisticsForSession(context, teamConnection, leaderSession);
        var frame = LegacyTeamArenaSignupResponsePacket.FromRequest(
                new LegacyTeamArenaSignupPacket(
                    LegacyTeamArenaSignupDialogType.Dialog2,
                    result),
                leaderSession.Character,
                leaderStatistics)
            .ToFrameBytes();

        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        foreach (var connection in teamStates.GetConnections(teamConnection))
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await SendTeamArenaInfoToTeamAsync(
                context,
                teamConnection,
                LegacyTeamArenaInfoStatus.NotSignedUp,
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendTeamArenaInfoToTeamAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        LegacyTeamArenaInfoStatus status,
        CancellationToken cancellationToken)
    {
        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(teamConnection))
        {
            if (context.Sessions?.TryGet(connection, out var session) != true ||
                session is null)
            {
                continue;
            }

            var statistics = GetTeamArenaStatisticsForSession(context, connection, session);
            await connection.SendAsync(
                    LegacyTeamArenaInfoPacket.FromStatistics(statistics, status).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }


    private static bool TryCreateTeamArenaInfoPlayersPacket(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        out LegacyTeamArenaInfoPlayersPacket packet)
    {
        var entries = new List<LegacyTeamArenaInfoPlayerEntry>();
        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(teamConnection))
        {
            if (context.Sessions?.TryGet(connection, out var session) != true ||
                session is null)
            {
                continue;
            }

            var statistics = GetTeamArenaStatisticsForSession(context, connection, session);
            entries.Add(new LegacyTeamArenaInfoPlayerEntry(
                CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
                CheckedUInt32(session.Character.Level, nameof(session.Character.Level)),
                CheckedUInt32(session.Character.Class, nameof(session.Character.Class)),
                BuildLegacyCharacterMesh(session.Character),
                statistics.TodayRank,
                statistics.ArenaPoints,
                session.Character.Name));
        }

        var leaderUid = CheckedUInt32(
            (context.TeamStates ?? new TeamSessionStateStore()).GetLeaderUid(teamConnection) ?? 0,
            "LeaderUid");
        packet = new LegacyTeamArenaInfoPlayersPacket(
            (uint)LegacyTeamArenaInfoPlayerKind.Opponents,
            leaderUid,
            checked((uint)entries.Count),
            entries);
        return entries.Count > 0 && leaderUid != 0;
    }


    private static async ValueTask<bool> HasTeamArenaLeaderAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string action,
        bool logWhenMissing)
    {
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        if (!teamStates.HasTeam(context.Connection))
        {
            if (logWhenMissing)
            {
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped team arena {action} {FormatSession(context)}; no team is available.")
                    .ConfigureAwait(false);
            }

            return false;
        }

        if (!teamStates.IsLeader(context.Connection, session.Character.Uid))
        {
            if (logWhenMissing)
            {
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped team arena {action} {FormatSession(context)}; session is not team leader.")
                    .ConfigureAwait(false);
            }

            return false;
        }

        return true;
    }


    private static async ValueTask HandleTeamArenaBuyPointsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        TeamArenaStatistics statistics,
        CancellationToken cancellationToken)
    {
        const long cost = 9_000_000;
        const uint points = 1_500;

        if (statistics.ArenaPoints > points || session.Character.Money < cost)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyTeamArenaSignup BuyPoints " +
                    $"{FormatSession(context)} money={session.Character.Money} teamArenaPoints={statistics.ArenaPoints}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = session.Character with { Money = session.Character.Money - cost };
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        teamArenaStates.AddPurchasedPoints(context.Connection, points);

        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.Money,
                checked((ulong)updatedCharacter.Money),
                cancellationToken)
            .ConfigureAwait(false);

        var arenaStatistics = GetArenaStatisticsForSession(context, updatedSession);
        await SendArenaInfoAsync(context, arenaStatistics, MsgArenaInfoStatus.NotSignedUp, cancellationToken)
            .ConfigureAwait(false);
    }
}
