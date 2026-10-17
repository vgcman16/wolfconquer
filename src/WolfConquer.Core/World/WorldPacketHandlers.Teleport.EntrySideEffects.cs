using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal readonly record struct LegacyTeleportRequest(
    string SourceMethod,
    long RequestedMapId,
    long RequestedDynamicId = 0,
    bool CanTeleport = false);

internal enum LegacyTeleportDenialKind
{
    EntrySideEffectPublicationUnavailable,
    StaleSourceSession,
    SourceMapGuard,
    CrossServerGuard,
}

internal readonly record struct LegacyTeleportDenial(
    LegacyTeleportDenialKind Kind,
    string Reason,
    string SourceEvidence);

internal readonly record struct LegacyTeleportPreflightOutcome(
    LegacyTeleportRequest Request,
    WorldSession Session,
    LegacyTeleportDenial? Denial,
    bool QualifierEndRequested,
    bool EndedQualifier,
    bool RemovedRide,
    bool InQualifier,
    bool IsWatching)
{
    public bool Allowed => Denial is null;
}

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyTeleportEntrySideEffectResult(
        WorldSession Session,
        LegacyTeleportDenial? Denial,
        bool QualifierEndRequested,
        bool EndedQualifier,
        bool RemovedRide);

    private static async ValueTask<LegacyTeleportEntrySideEffectResult> ApplyLegacyTeleportEntrySideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession sourceSession,
        LegacyTeleportRequest request,
        CancellationToken cancellationToken)
    {
        var qualifierEndRequested =
            IsLegacyTeleportInQualifier(context, sourceSession.Character) &&
            request.RequestedMapId != LegacyArenaMapId &&
            request.RequestedDynamicId != 0;
        var endedQualifier = qualifierEndRequested && EndLegacyTeleportQualifier(context, sourceSession);

        var removedRide = request.RequestedMapId == LegacyArenaMapId &&
            HasLegacyStatusFlag(sourceSession.Character, LegacyRideFlag);
        if (!removedRide)
        {
            await LogLegacyTeleportEntrySideEffectsAsync(
                    context,
                    sourceSession,
                    request,
                    qualifierEndRequested,
                    endedQualifier,
                    removedRide: false)
                .ConfigureAwait(false);
            return new LegacyTeleportEntrySideEffectResult(
                sourceSession,
                Denial: null,
                qualifierEndRequested,
                endedQualifier,
                RemovedRide: false);
        }

        if (context.Sessions is not { } sessions)
        {
            return new LegacyTeleportEntrySideEffectResult(
                sourceSession,
                new LegacyTeleportDenial(
                    LegacyTeleportDenialKind.EntrySideEffectPublicationUnavailable,
                    "Ride removal requires an available WorldSessionStore",
                    "GameClient.Teleport lines 1733-1735; target CAS publication convention"),
                qualifierEndRequested,
                endedQualifier,
                RemovedRide: false);
        }

        var updatedSession = sourceSession with
        {
            Character = ClearLegacyMountRideStatus(sourceSession.Character),
        };
        if (!sessions.TryCompareAndSet(context.Connection, sourceSession, updatedSession))
        {
            sessions.TryGet(context.Connection, out var currentSession);
            return new LegacyTeleportEntrySideEffectResult(
                currentSession ?? sourceSession,
                new LegacyTeleportDenial(
                    LegacyTeleportDenialKind.StaleSourceSession,
                    "Ride removal compare-and-set rejected a stale source session",
                    "GameClient.Teleport lines 1733-1735; Teleport.StatusCleanup.cs lines 116-163"),
                qualifierEndRequested,
                endedQualifier,
                RemovedRide: false);
        }

        var updatedContext = context with { Session = updatedSession };
        await SendPlayerStatusFlagVectorToVisibleAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        await LogLegacyTeleportEntrySideEffectsAsync(
                updatedContext,
                updatedSession,
                request,
                qualifierEndRequested,
                endedQualifier,
                removedRide: true)
            .ConfigureAwait(false);
        return new LegacyTeleportEntrySideEffectResult(
            updatedSession,
            Denial: null,
            qualifierEndRequested,
            endedQualifier,
            RemovedRide: true);
    }

    internal static WorldSession CarryLegacyTeleportEntrySideEffects(
        LegacyTeleportPreflightOutcome outcome,
        WorldSession destinationSession)
    {
        if (!outcome.RemovedRide)
        {
            return destinationSession;
        }

        return destinationSession with
        {
            Character = ClearLegacyMountRideStatus(destinationSession.Character),
        };
    }

    private static bool EndLegacyTeleportQualifier(
        WorldPacketHandlerContext context,
        WorldSession sourceSession)
    {
        var endedAtUtc = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
        var endedQualifier = false;
        if (context.ArenaStates is { } arenaStates &&
            arenaStates.TryEndActiveMatchAsLoser(context.Connection, out _, endedAtUtc))
        {
            endedQualifier = true;
        }

        if (TryFindLegacyTeamArenaQualifierTeam(context, context.Connection, out var teamArenaConnection))
        {
            var teamConnections = GetLegacyQualifierTeamConnections(context, teamArenaConnection);
            if (AreLegacyTeamQualifierMembersDead(context, teamConnections))
            {
                endedQualifier |= context.TeamArenaStates!.TryEndActiveMatchAsLoser(
                    teamArenaConnection,
                    out _,
                    endedAtUtc);
            }

            if (IsLegacyQualifierTeamLeader(context, sourceSession, teamArenaConnection) &&
                teamConnections.Count <= 1)
            {
                endedQualifier |= context.TeamArenaStates!.TryEndActiveMatchAsLoser(
                    teamArenaConnection,
                    out _,
                    endedAtUtc);
                return endedQualifier;
            }

            if (IsLegacyQualifierTeamDeadOnMap(context, teamConnections, LegacyArenaMapId))
            {
                endedQualifier |= context.TeamArenaStates!.TryEndActiveMatchAsLoser(
                    teamArenaConnection,
                    out _,
                    endedAtUtc);
            }
        }

        if (context.ElitePkStates is { } elitePkStates &&
            elitePkStates.TryEndActiveMatchAsLoser(context.Connection, endedAtUtc))
        {
            endedQualifier = true;
        }

        if (TryFindLegacyTeamPkQualifierTeam(context, context.Connection, out var teamPkConnection))
        {
            var teamConnections = GetLegacyQualifierTeamConnections(context, teamPkConnection);
            if (IsLegacyQualifierTeamLeader(context, sourceSession, teamPkConnection) &&
                teamConnections.Count <= 1)
            {
                endedQualifier |= context.TeamPkStates!.TryEndActiveMatchAsLoser(
                    teamPkConnection,
                    endedAtUtc);
            }
            else if (IsLegacyQualifierTeamDeadOnMap(context, teamConnections, LegacyArenaMapId))
            {
                endedQualifier |= context.TeamPkStates!.TryEndActiveMatchAsLoser(
                    teamPkConnection,
                    endedAtUtc);
            }
        }

        return endedQualifier;
    }

    private static bool IsLegacyTeleportInQualifier(
        WorldPacketHandlerContext context,
        CharacterSnapshot character)
    {
        return context.ArenaStates?.IsQualifierFighter(context.Connection) == true ||
            IsLegacyTeamArenaQualifierMember(context, context.Connection) ||
            context.ElitePkStates?.IsQualifierFighter(context.Connection) == true ||
            IsLegacyTeamPkQualifierMember(context, context.Connection) ||
            IsLegacySpaceLeapQualifierBlocked(character);
    }

    private static bool IsLegacyTeleportWatching(
        WorldPacketHandlerContext context,
        CharacterSnapshot character)
    {
        return context.ArenaStates?.IsWatcher(context.Connection) == true ||
            context.TeamArenaStates?.IsWatcher(context.Connection) == true ||
            HasLegacyPlayerMagicWatchingAttribute(character);
    }

    private static bool IsLegacyTeamArenaQualifierMember(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection)
    {
        return TryFindLegacyTeamArenaQualifierTeam(context, connection, out _);
    }

    private static bool IsLegacyTeamPkQualifierMember(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection)
    {
        return TryFindLegacyTeamPkQualifierTeam(context, connection, out _);
    }

    private static bool TryFindLegacyTeamArenaQualifierTeam(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        out ConquerTcpConnection teamConnection)
    {
        if (context.TeamArenaStates is { } teamArenaStates)
        {
            foreach (var match in teamArenaStates.GetActiveMatches())
            {
                if (LegacyQualifierTeamContainsConnection(context, match.First, connection))
                {
                    teamConnection = match.First;
                    return true;
                }

                if (LegacyQualifierTeamContainsConnection(context, match.Second, connection))
                {
                    teamConnection = match.Second;
                    return true;
                }
            }
        }

        teamConnection = null!;
        return false;
    }

    private static bool TryFindLegacyTeamPkQualifierTeam(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        out ConquerTcpConnection teamConnection)
    {
        if (context.TeamPkStates is { } teamPkStates)
        {
            foreach (var match in teamPkStates.GetActiveMatches())
            {
                if (LegacyQualifierTeamContainsConnection(context, match.First, connection))
                {
                    teamConnection = match.First;
                    return true;
                }

                if (LegacyQualifierTeamContainsConnection(context, match.Second, connection))
                {
                    teamConnection = match.Second;
                    return true;
                }
            }
        }

        teamConnection = null!;
        return false;
    }

    private static bool LegacyQualifierTeamContainsConnection(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection,
        ConquerTcpConnection connection)
    {
        if (ReferenceEquals(teamConnection, connection))
        {
            return true;
        }

        return context.TeamStates?.GetConnections(teamConnection)
            .Any(member => ReferenceEquals(member, connection)) == true;
    }

    private static IReadOnlyList<ConquerTcpConnection> GetLegacyQualifierTeamConnections(
        WorldPacketHandlerContext context,
        ConquerTcpConnection teamConnection)
    {
        var connections = context.TeamStates?.GetConnections(teamConnection);
        return connections is { Count: > 0 } ? connections : [teamConnection];
    }

    private static bool AreLegacyTeamQualifierMembersDead(
        WorldPacketHandlerContext context,
        IReadOnlyList<ConquerTcpConnection> teamConnections)
    {
        foreach (var connection in teamConnections)
        {
            if (!IsLegacyTeamArenaQualifierMember(context, connection) &&
                !IsLegacyTeamPkQualifierMember(context, connection))
            {
                continue;
            }

            if (context.Sessions?.TryGet(connection, out var session) == true &&
                session is not null &&
                IsLegacyTeleportCharacterAlive(session.Character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLegacyQualifierTeamDeadOnMap(
        WorldPacketHandlerContext context,
        IReadOnlyList<ConquerTcpConnection> teamConnections,
        long mapId)
    {
        foreach (var connection in teamConnections)
        {
            if (context.Sessions?.TryGet(connection, out var session) == true &&
                session is not null &&
                session.Character.MapId == mapId &&
                IsLegacyTeleportCharacterAlive(session.Character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLegacyQualifierTeamLeader(
        WorldPacketHandlerContext context,
        WorldSession sourceSession,
        ConquerTcpConnection teamConnection)
    {
        return context.TeamStates?.IsLeader(context.Connection, sourceSession.Character.Uid) ??
            ReferenceEquals(context.Connection, teamConnection);
    }

    private static bool IsLegacyTeleportCharacterAlive(CharacterSnapshot character)
    {
        return character.MinHitPoints > 0 &&
            !LegacyPlayerDeathState.HasStatusFlag(
                character.Attributes,
                LegacyPlayerDeathState.DeadStatusFlag);
    }

    private static async ValueTask LogLegacyTeleportEntrySideEffectsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyTeleportRequest request,
        bool qualifierEndRequested,
        bool endedQualifier,
        bool removedRide)
    {
        if (!qualifierEndRequested && !removedRide)
        {
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied ordered legacy GameClient.Teleport entry side effects " +
                $"{FormatSession(context with { Session = session })} source={request.SourceMethod} " +
                $"requestedMap={request.RequestedMapId} requestedDynamic={request.RequestedDynamicId} " +
                $"qualifierEndRequested={qualifierEndRequested} endedQualifier={endedQualifier} " +
                $"removedRide={removedRide}; source=GameClient.Teleport lines 1729-1735.")
            .ConfigureAwait(false);
    }
}
