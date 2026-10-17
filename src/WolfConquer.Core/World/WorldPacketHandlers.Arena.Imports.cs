using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendArenaImportAsync(
        WorldPacketHandlerContext context,
        ArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        await ImportArenaPlayerAsync(
                context,
                match.First,
                LegacyArenaFirstPlayerX,
                LegacyArenaFirstPlayerY,
                cancellationToken)
            .ConfigureAwait(false);
        await ImportArenaPlayerAsync(
                context,
                match.Second,
                LegacyArenaSecondPlayerX,
                LegacyArenaSecondPlayerY,
                cancellationToken)
            .ConfigureAwait(false);
        await SendArenaOpponentImportAsync(context, sourceConnection: match.First, targetConnection: match.Second, cancellationToken)
            .ConfigureAwait(false);
        await SendArenaOpponentImportAsync(context, sourceConnection: match.Second, targetConnection: match.First, cancellationToken)
            .ConfigureAwait(false);
        await SendArenaMatchScoreAsync(context, match, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer sent Arena import frames {FormatSession(context)}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask ImportArenaPlayerAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        long x,
        long y,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(connection, out var session) != true ||
            session is null)
        {
            return;
        }

        var playerContext = context with { Connection = connection, Session = session };
        var teleportRequest = new LegacyTeleportRequest(
            "MsgArena.ArenaMatch.Import -> GameClient.Teleport",
            LegacyArenaMapId);
        var preflight = await PrepareLegacyTeleportAsync(
                playerContext,
                session,
                teleportRequest,
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        session = preflight.Session;
        var importedSession = await ExecuteLegacyTeleportDestinationAsync(
                playerContext with { Session = session },
                session,
                new LegacyTeleportDestination(
                    teleportRequest.RequestedMapId,
                    x,
                    y,
                    checked((ulong)teleportRequest.RequestedDynamicId)),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (importedSession is null)
        {
            return;
        }
    }


    private static async ValueTask ReturnArenaPlayerAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(connection, out var session) != true ||
            session is null)
        {
            return;
        }

        var character = session.Character;
        if (character.MapId != LegacyArenaMapId)
        {
            return;
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

        context.Sessions.Set(connection, session with { Character = returned });
        await SendLegacySetLocationAsync(context, connection, returned, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendArenaOpponentImportAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection sourceConnection,
        ConquerTcpConnection targetConnection,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(sourceConnection, out var sourceSession) != true ||
            sourceSession is null)
        {
            return;
        }

        var statistics = GetArenaStatisticsForSession(context, sourceSession);
        var startFight = MsgArenaSignupResponsePacket.FromRequest(
                new MsgArenaSignupPacket(
                    MsgArenaSignupDialogType.StartTheFight,
                    MsgArenaSignupDialogButton.SignUp),
                sourceSession.Character,
                statistics)
            .ToFrameBytes();
        var matchOn = MsgArenaSignupResponsePacket.FromRequest(
                new MsgArenaSignupPacket(
                    MsgArenaSignupDialogType.Match,
                    MsgArenaSignupDialogButton.MatchOn),
                sourceSession.Character,
                statistics)
            .ToFrameBytes();

        await targetConnection.SendAsync(startFight, cancellationToken).ConfigureAwait(false);
        await targetConnection.SendAsync(matchOn, cancellationToken).ConfigureAwait(false);
    }


    private static async ValueTask SendArenaMatchScoreAsync(
        WorldPacketHandlerContext context,
        ArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(match.First, out var firstSession) != true ||
            firstSession is null ||
            context.Sessions?.TryGet(match.Second, out var secondSession) != true ||
            secondSession is null)
        {
            return;
        }

        var firstDamage = 0U;
        var secondDamage = 0U;
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (arenaStates.TryGetScoreUpdate(match, out var scoreUpdate))
        {
            firstDamage = scoreUpdate.FirstDamage;
            secondDamage = scoreUpdate.SecondDamage;
        }

        var watchers = Array.Empty<ConquerTcpConnection>();
        if (arenaStates.TryGetWatcherUpdate(match, out var watcherUpdate))
        {
            watchers = watcherUpdate.Watchers.ToArray();
        }

        var frame = LegacyArenaMatchScorePacket.Create(
                CheckedUInt32(firstSession.Character.Uid, nameof(firstSession.Character.Uid)),
                LegacyArenaServerId,
                firstSession.Character.Name,
                firstDamage,
                CheckedUInt32(secondSession.Character.Uid, nameof(secondSession.Character.Uid)),
                LegacyArenaServerId,
                secondSession.Character.Name,
                secondDamage)
            .ToFrameBytes();

        await match.First.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        await match.Second.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        foreach (var watcher in watchers)
        {
            await watcher.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }


    private static async ValueTask SendTeamArenaImportAsync(
        WorldPacketHandlerContext context,
        TeamArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        await ImportTeamArenaTeamAsync(
                context,
                match.First,
                LegacyTeamArenaFirstTeamX,
                LegacyTeamArenaFirstTeamY,
                cancellationToken)
            .ConfigureAwait(false);
        await ImportTeamArenaTeamAsync(
                context,
                match.Second,
                LegacyTeamArenaSecondTeamX,
                LegacyTeamArenaSecondTeamY,
                cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaOpponentImportAsync(context, sourceTeamConnection: match.First, targetTeamConnection: match.Second, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaOpponentImportAsync(context, sourceTeamConnection: match.Second, targetTeamConnection: match.First, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaMatchScoreAsync(context, match, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyTeamArena import frames {FormatSession(context)}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask ImportTeamArenaTeamAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        long x,
        long y,
        CancellationToken cancellationToken)
    {
        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(teamConnection))
        {
            if (context.Sessions?.TryGet(connection, out var session) != true ||
                session is null)
            {
                continue;
            }

            var playerContext = context with { Connection = connection, Session = session };
            var teleportRequest = new LegacyTeleportRequest(
                "MsgTeamArena.TeamArenaMatch.Import -> GameClient.Teleport",
                LegacyTeamArenaMapId);
            var preflight = await PrepareLegacyTeleportAsync(
                    playerContext,
                    session,
                    teleportRequest,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!preflight.Allowed)
            {
                continue;
            }

            session = preflight.Session;
            var importedSession = await ExecuteLegacyTeleportDestinationAsync(
                    playerContext with { Session = session },
                    session,
                    new LegacyTeleportDestination(
                        teleportRequest.RequestedMapId,
                        x,
                        y,
                        checked((ulong)teleportRequest.RequestedDynamicId)),
                    timestamp: 0,
                    cancellationToken)
                .ConfigureAwait(false);
            if (importedSession is null)
            {
                continue;
            }
        }
    }


    private static async ValueTask ReturnTeamArenaTeamAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        CancellationToken cancellationToken)
    {
        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(teamConnection))
        {
            if (context.Sessions?.TryGet(connection, out var session) != true ||
                session is null)
            {
                continue;
            }

            var character = session.Character;
            if (character.MapId != LegacyTeamArenaMapId)
            {
                continue;
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

            context.Sessions.Set(connection, session with { Character = returned });
            await SendLegacySetLocationAsync(context, connection, returned, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendTeamArenaOpponentImportAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection sourceTeamConnection,
        ConquerTcpConnection targetTeamConnection,
        CancellationToken cancellationToken)
    {
        if (!TryCreateTeamArenaInfoPlayersPacket(context, sourceTeamConnection, out var sourceTeam) ||
            !TryGetTeamLeaderSession(context, sourceTeamConnection, out var sourceLeader))
        {
            return;
        }

        var statistics = GetTeamArenaStatisticsForSession(context, sourceLeader.Connection, sourceLeader.Session);
        var infoPlayers = sourceTeam.ToFrameBytes();
        var startFight = LegacyTeamArenaSignupResponsePacket.FromRequest(
                new LegacyTeamArenaSignupPacket(
                    LegacyTeamArenaSignupDialogType.StartTheFight,
                    LegacyTeamArenaSignupDialogButton.SignUp),
                sourceLeader.Session.Character,
                statistics)
            .ToFrameBytes();
        var matchOn = LegacyTeamArenaSignupResponsePacket.FromRequest(
                new LegacyTeamArenaSignupPacket(
                    LegacyTeamArenaSignupDialogType.Match,
                    LegacyTeamArenaSignupDialogButton.MatchOn),
                sourceLeader.Session.Character,
                statistics)
            .ToFrameBytes();

        foreach (var connection in (context.TeamStates ?? new TeamSessionStateStore()).GetConnections(targetTeamConnection))
        {
            await connection.SendAsync(infoPlayers, cancellationToken).ConfigureAwait(false);
            await connection.SendAsync(startFight, cancellationToken).ConfigureAwait(false);
            await connection.SendAsync(matchOn, cancellationToken).ConfigureAwait(false);
        }
    }


    private static async ValueTask SendTeamArenaMatchScoreAsync(
        WorldPacketHandlerContext context,
        TeamArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeamLeaderSession(context, match.First, out var firstLeader) ||
            !TryGetTeamLeaderSession(context, match.Second, out var secondLeader))
        {
            return;
        }

        var firstDamage = 0U;
        var secondDamage = 0U;
        IReadOnlyList<ConquerTcpConnection> watchers = [];
        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (teamArenaStates.TryGetScoreUpdate(match, out var scoreUpdate))
        {
            firstDamage = scoreUpdate.FirstDamage;
            secondDamage = scoreUpdate.SecondDamage;
            watchers = scoreUpdate.Watchers;
        }

        var firstStatistics = GetTeamArenaStatisticsForSession(context, firstLeader.Connection, firstLeader.Session);
        var secondStatistics = GetTeamArenaStatisticsForSession(context, secondLeader.Connection, secondLeader.Session);
        var frame = LegacyTeamArenaMatchScorePacket.Create(
                CheckedUInt32(firstLeader.Session.Character.Uid, nameof(firstLeader.Session.Character.Uid)),
                firstStatistics.TodayRank,
                firstLeader.Session.Character.Name,
                firstDamage,
                CheckedUInt32(secondLeader.Session.Character.Uid, nameof(secondLeader.Session.Character.Uid)),
                secondStatistics.TodayRank,
                secondLeader.Session.Character.Name,
                secondDamage)
            .ToFrameBytes();

        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        foreach (var connection in teamStates.GetConnections(match.First))
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        foreach (var connection in teamStates.GetConnections(match.Second))
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        foreach (var connection in watchers)
        {
            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
