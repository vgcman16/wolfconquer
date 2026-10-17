using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendArenaWatcherRequestAsync(
        WorldPacketHandlerContext context,
        WorldSession watcherSession,
        ArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        if (IsLegacyArenaWatcherBusy(context, context.Connection))
        {
            return;
        }

        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (!arenaStates.TryAddWatcher(context.Connection, match, out var update))
        {
            return;
        }

        await ImportArenaWatcherAsync(context, watcherSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyArenaWatchersPacket.Create(
                    LegacyArenaWatcherMode.RequestView,
                    dwParam: 0,
                    uid: 0,
                    count: 0,
                    player1Cheers: 0,
                    player2Cheers: 0,
                    entries: []).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var watchers = CreateArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);
        await SendArenaFrameToWatchersAsync(update, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendArenaFrameToFightersAsync(update.Match, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyArenaWatchers Arena request-view {FormatSession(context)}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask<bool> HandleArenaWatcherQuitAsync(
        WorldPacketHandlerContext context,
        WorldSession watcherSession,
        CancellationToken cancellationToken)
    {
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (!arenaStates.TryRemoveWatcher(context.Connection, out var update))
        {
            return false;
        }

        await context.Connection.SendAsync(
                CreateArenaWatchersFrame(context, LegacyArenaWatcherMode.Leave, update),
                cancellationToken)
            .ConfigureAwait(false);

        var watchers = CreateArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);
        await SendArenaFrameToWatchersAsync(update, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendArenaFrameToFightersAsync(update.Match, watchers, cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
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
        await SendArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);
        await ReturnArenaPlayerAsync(context, context.Connection, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }


    private static async ValueTask HandleArenaWatcherCheerAsync(
        WorldPacketHandlerContext context,
        uint fighterUid,
        CancellationToken cancellationToken)
    {
        if (!TryFindActiveArenaMatchByFighterUid(context, fighterUid, out var match))
        {
            return;
        }

        var firstFighter = ArenaConnectionHasUid(context, match.First, fighterUid);
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (!arenaStates.TryCheer(context.Connection, match, firstFighter, out var update))
        {
            return;
        }

        var watchers = CreateArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);
        await SendArenaFrameToWatchersAsync(update, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendArenaFrameToFightersAsync(update.Match, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask ImportArenaWatcherAsync(
        WorldPacketHandlerContext context,
        WorldSession watcherSession,
        CancellationToken cancellationToken)
    {
        var teleportRequest = new LegacyTeleportRequest(
            "MsgArena.ArenaMatch.DoWatching -> GameClient.Teleport",
            LegacyArenaMapId);
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                watcherSession,
                teleportRequest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        watcherSession = preflight.Session;
        var importedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = watcherSession },
                watcherSession,
                new LegacyTeleportDestination(
                    teleportRequest.RequestedMapId,
                    Random.Shared.Next(LegacyArenaWatcherMinCoordinate, LegacyArenaWatcherMaxExclusiveCoordinate),
                    Random.Shared.Next(LegacyArenaWatcherMinCoordinate, LegacyArenaWatcherMaxExclusiveCoordinate),
                    checked((ulong)teleportRequest.RequestedDynamicId)),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (importedSession is null)
        {
            return;
        }
    }


    private static async ValueTask SendArenaFrameToFightersAsync(
        ArenaMatchPair match,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        await match.First.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        await match.Second.SendAsync(frame, cancellationToken).ConfigureAwait(false);
    }


    private static async ValueTask SendArenaFrameToWatchersAsync(
        ArenaWatcherUpdate update,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        foreach (var connection in update.Watchers)
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static async ValueTask ExportArenaCompletedWatchersAsync(
        WorldPacketHandlerContext context,
        ArenaCompletedMatch completed,
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
            var update = new ArenaWatcherUpdate(
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
                        CreateArenaWatchersFrame(context, LegacyArenaWatcherMode.Leave, update),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var watchers = CreateArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);
            await SendArenaFrameToWatchersAsync(update, watchers, cancellationToken)
                .ConfigureAwait(false);
            await SendArenaFrameToFightersAsync(completed.Match, watchers, cancellationToken)
                .ConfigureAwait(false);
            await SendArenaCompletedMatchScoreAsync(context, completed, update.Watchers, cancellationToken)
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
            await ReturnArenaPlayerAsync(context, watcher, cancellationToken)
                .ConfigureAwait(false);
        }
    }


    private static async ValueTask SendArenaCompletedMatchScoreAsync(
        WorldPacketHandlerContext context,
        ArenaCompletedMatch completed,
        IReadOnlyList<ConquerTcpConnection> watchers,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(completed.Match.First, out var firstSession) != true ||
            firstSession is null ||
            context.Sessions?.TryGet(completed.Match.Second, out var secondSession) != true ||
            secondSession is null)
        {
            return;
        }

        var frame = LegacyArenaMatchScorePacket.Create(
                CheckedUInt32(firstSession.Character.Uid, nameof(firstSession.Character.Uid)),
                LegacyArenaServerId,
                firstSession.Character.Name,
                completed.FirstDamage,
                CheckedUInt32(secondSession.Character.Uid, nameof(secondSession.Character.Uid)),
                LegacyArenaServerId,
                secondSession.Character.Name,
                completed.SecondDamage)
            .ToFrameBytes();

        await completed.Match.First.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        await completed.Match.Second.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        foreach (var watcher in watchers)
        {
            await watcher.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static byte[] CreateArenaWatchersFrame(
        WorldPacketHandlerContext context,
        LegacyArenaWatcherMode mode,
        ArenaWatcherUpdate update)
    {
        var entries = new List<LegacyArenaWatcherEntry>();
        foreach (var connection in update.Watchers)
        {
            if (context.Sessions?.TryGet(connection, out var session) != true ||
                session is null)
            {
                continue;
            }

            entries.Add(CreateArenaWatcherEntry(context, connection, session, useTeamArenaStatistics: false));
        }

        return LegacyArenaWatchersPacket.Create(
                mode,
                dwParam: 0,
                uid: 0,
                checked((uint)entries.Count),
                update.FirstCheers,
                update.SecondCheers,
                entries)
            .ToFrameBytes();
    }


    private static async ValueTask SendTeamArenaWatcherRequestAsync(
        WorldPacketHandlerContext context,
        WorldSession watcherSession,
        TeamArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        if (IsLegacyArenaWatcherBusy(context, context.Connection))
        {
            return;
        }

        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (!teamArenaStates.TryAddWatcher(context.Connection, match, out var update))
        {
            return;
        }

        await ImportTeamArenaWatcherAsync(context, watcherSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyArenaWatchersPacket.Create(
                    LegacyArenaWatcherMode.RequestView,
                    dwParam: 0,
                    uid: 0,
                    count: 0,
                    player1Cheers: 0,
                    player2Cheers: 0,
                    entries: []).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var watchers = CreateTeamArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);

        await context.Connection.SendAsync(watchers, cancellationToken).ConfigureAwait(false);
        await SendTeamArenaFrameToTeamAsync(context, update.Match.First, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFrameToTeamAsync(context, update.Match.Second, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyArenaWatchers TeamArena request-view {FormatSession(context)}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask HandleTeamArenaWatcherQuitAsync(
        WorldPacketHandlerContext context,
        WorldSession watcherSession,
        CancellationToken cancellationToken)
    {
        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (!teamArenaStates.TryRemoveWatcher(context.Connection, out var update))
        {
            return;
        }

        await context.Connection.SendAsync(
                CreateTeamArenaWatchersFrame(context, LegacyArenaWatcherMode.Leave, update),
                cancellationToken)
            .ConfigureAwait(false);

        var watchers = CreateTeamArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);
        await SendTeamArenaFrameToWatchersAsync(context, update, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFrameToTeamAsync(context, update.Match.First, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFrameToTeamAsync(context, update.Match.Second, watchers, cancellationToken)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
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
        await SendTeamArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);
        await ReturnTeamArenaWatcherAsync(context, context.Connection, watcherSession, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask HandleTeamArenaWatcherCheerAsync(
        WorldPacketHandlerContext context,
        uint fighterUid,
        CancellationToken cancellationToken)
    {
        if (!TryFindActiveTeamArenaMatchByMemberUid(context, fighterUid, out var match))
        {
            return;
        }

        var firstTeam = TeamArenaTeamContainsUid(context, match.First, fighterUid);
        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (!teamArenaStates.TryCheer(context.Connection, match, firstTeam, out var update))
        {
            return;
        }

        var watchers = CreateTeamArenaWatchersFrame(context, LegacyArenaWatcherMode.Watchers, update);
        await SendTeamArenaFrameToWatchersAsync(context, update, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFrameToTeamAsync(context, update.Match.First, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFrameToTeamAsync(context, update.Match.Second, watchers, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask ImportTeamArenaWatcherAsync(
        WorldPacketHandlerContext context,
        WorldSession watcherSession,
        CancellationToken cancellationToken)
    {
        var teleportRequest = new LegacyTeleportRequest(
            "MsgTeamArena.TeamArenaMatch.DoWatching -> GameClient.Teleport",
            LegacyTeamArenaMapId);
        var preflight = await PrepareLegacyTeleportAsync(
                context,
                watcherSession,
                teleportRequest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        watcherSession = preflight.Session;
        var importedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = watcherSession },
                watcherSession,
                new LegacyTeleportDestination(
                    teleportRequest.RequestedMapId,
                    LegacyTeamArenaWatcherX,
                    LegacyTeamArenaWatcherY,
                    checked((ulong)teleportRequest.RequestedDynamicId)),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (importedSession is null)
        {
            return;
        }
    }


    private static async ValueTask ReturnTeamArenaWatcherAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection watcherConnection,
        WorldSession watcherSession,
        CancellationToken cancellationToken)
    {
        var character = watcherSession.Character;
        var sessionToUpdate = watcherSession;
        if (context.Sessions?.TryGet(watcherConnection, out var currentSession) == true &&
            currentSession is not null)
        {
            character = currentSession.Character;
            sessionToUpdate = currentSession;
        }

        var returnMap = character.PreviousMapId == 0 ? character.MapId : character.PreviousMapId;
        var returnX = character.PreviousMapX == 0 ? character.X : character.PreviousMapX;
        var returnY = character.PreviousMapY == 0 ? character.Y : character.PreviousMapY;
        var returned = character with
        {
            MapId = returnMap,
            X = returnX,
            Y = returnY,
        };

        context.Sessions?.Set(watcherConnection, sessionToUpdate with { Character = returned });
        await SendLegacySetLocationAsync(context, watcherConnection, returned, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendTeamArenaFrameToTeamAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(teamConnection))
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static async ValueTask SendTeamArenaFrameToWatchersAsync(
        WorldPacketHandlerContext context,
        TeamArenaWatcherUpdate update,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        foreach (var connection in update.Watchers)
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static byte[] CreateTeamArenaWatchersFrame(
        WorldPacketHandlerContext context,
        LegacyArenaWatcherMode mode,
        TeamArenaWatcherUpdate update)
    {
        var entries = new List<LegacyArenaWatcherEntry>();
        foreach (var connection in update.Watchers)
        {
            if (context.Sessions?.TryGet(connection, out var session) != true ||
                session is null)
            {
                continue;
            }

            entries.Add(CreateArenaWatcherEntry(context, connection, session, useTeamArenaStatistics: true));
        }

        return LegacyArenaWatchersPacket.Create(
                mode,
                dwParam: 0,
                uid: 0,
                checked((uint)entries.Count),
                update.FirstCheers,
                update.SecondCheers,
                entries)
            .ToFrameBytes();
    }


    private static LegacyArenaWatcherEntry CreateArenaWatcherEntry(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        bool useTeamArenaStatistics)
    {
        var todayRank = useTeamArenaStatistics
            ? GetTeamArenaStatisticsForSession(context, connection, session).TodayRank
            : GetArenaStatisticsForSession(context, connection, session).TodayRank;
        return new LegacyArenaWatcherEntry(
            BuildLegacyCharacterMesh(session.Character),
            session.Character.Name,
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            CheckedUInt32(session.Character.Level, nameof(session.Character.Level)),
            CheckedUInt32(session.Character.Class, nameof(session.Character.Class)),
            todayRank);
    }


    private static bool IsLegacyArenaWatcherBusy(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection)
    {
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (arenaStates.IsActiveFighterOrWatcher(connection))
        {
            return true;
        }

        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        return teamArenaStates.IsActiveFighterOrWatcher(connection);
    }


    private static bool TryFindActiveArenaMatchByFighterUid(
        WorldPacketHandlerContext context,
        uint uid,
        out ArenaMatchPair match)
    {
        foreach (var activeMatch in (context.ArenaStates ?? new ArenaSessionStateStore()).GetActiveMatches())
        {
            if (ArenaConnectionHasUid(context, activeMatch.First, uid) ||
                ArenaConnectionHasUid(context, activeMatch.Second, uid))
            {
                match = activeMatch;
                return true;
            }
        }

        match = default;
        return false;
    }


    private static bool ArenaConnectionHasUid(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        uint uid)
    {
        return context.Sessions?.TryGet(connection, out var session) == true &&
            session is not null &&
            session.Character.Uid == uid;
    }


    private static bool TryFindActiveTeamArenaMatchByMemberUid(
        WorldPacketHandlerContext context,
        uint uid,
        out TeamArenaMatchPair match)
    {
        foreach (var activeMatch in (context.TeamArenaStates ?? new TeamArenaSessionStateStore()).GetActiveMatches())
        {
            if (TeamArenaTeamContainsUid(context, activeMatch.First, uid) ||
                TeamArenaTeamContainsUid(context, activeMatch.Second, uid))
            {
                match = activeMatch;
                return true;
            }
        }

        match = default;
        return false;
    }


    private static bool TeamArenaTeamContainsUid(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        uint uid)
    {
        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(teamConnection))
        {
            if (context.Sessions?.TryGet(connection, out var session) == true &&
                session is not null &&
                session.Character.Uid == uid)
            {
                return true;
            }
        }

        return false;
    }
}
