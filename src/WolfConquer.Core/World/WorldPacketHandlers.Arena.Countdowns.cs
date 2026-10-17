using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleTeamArenaAcceptCountdownAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (!await HasTeamArenaLeaderAsync(context, session, "accept-countdown", logWhenMissing: true).ConfigureAwait(false))
        {
            return;
        }

        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (teamArenaStates.TryAcceptCountdown(context.Connection, out var match))
        {
            await SendTeamArenaImportAsync(context, match, cancellationToken)
                .ConfigureAwait(false);
        }
    }


    private static async ValueTask HandleArenaAcceptCountdownAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (arenaStates.TryCompleteDeclinedPendingMatch(context.Connection, out var declined))
        {
            await SendArenaPendingDeclineResultAsync(context, declined, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (arenaStates.TryAcceptCountdown(context.Connection, out var match))
        {
            await SendArenaImportAsync(context, match, cancellationToken)
                .ConfigureAwait(false);
        }
    }


    private static async ValueTask SendTeamArenaMatchCountdownAsync(
        WorldPacketHandlerContext context,
        TeamArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        await SendTeamArenaMatchCountdownAsync(context, match.First, cancellationToken).ConfigureAwait(false);
        await SendTeamArenaMatchCountdownAsync(context, match.Second, cancellationToken).ConfigureAwait(false);
    }


    private static async ValueTask SendTeamArenaMatchCountdownAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(connection, out var session) != true ||
            session is null)
        {
            return;
        }

        var statistics = GetTeamArenaStatisticsForSession(context, connection, session);
        await connection.SendAsync(
                LegacyTeamArenaInfoPacket.FromStatistics(
                    statistics,
                    LegacyTeamArenaInfoStatus.WaitingInactive).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                LegacyTeamArenaSignupResponsePacket.FromRequest(
                    new LegacyTeamArenaSignupPacket(
                        LegacyTeamArenaSignupDialogType.StartCountDown,
                        LegacyTeamArenaSignupDialogButton.SignUp),
                    session.Character,
                    statistics).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyTeamArena match countdown {FormatSession(context)} targetUid={session.Character.Uid}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask SendArenaMatchCountdownAsync(
        WorldPacketHandlerContext context,
        ArenaMatchPair match,
        CancellationToken cancellationToken)
    {
        await SendArenaMatchCountdownAsync(context, match.First, cancellationToken).ConfigureAwait(false);
        await SendArenaMatchCountdownAsync(context, match.Second, cancellationToken).ConfigureAwait(false);
    }


    private static async ValueTask SendArenaMatchCountdownAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        CancellationToken cancellationToken)
    {
        if (context.Sessions?.TryGet(connection, out var session) != true ||
            session is null)
        {
            return;
        }

        var statistics = GetArenaStatisticsForSession(context, session);
        await connection.SendAsync(
                MsgArenaInfoPacket.FromStatistics(
                    statistics,
                    MsgArenaInfoStatus.WaitingForOpponent).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                MsgArenaSignupResponsePacket.FromRequest(
                    new MsgArenaSignupPacket(
                        MsgArenaSignupDialogType.StartCountDown,
                        MsgArenaSignupDialogButton.SignUp),
                    session.Character,
                    statistics).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer sent Arena match countdown {FormatSession(context)} targetUid={session.Character.Uid}.")
            .ConfigureAwait(false);
    }
}
