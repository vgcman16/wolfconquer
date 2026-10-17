using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyTeamArenaInfo)]
    private static async ValueTask HandleLegacyTeamArenaInfoAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTeamArenaInfoPacket.IsQuery(frame))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyTeamArenaInfo packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyTeamArenaInfo {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var teamArenaStatistics = GetTeamArenaStatisticsForSession(context, session);
        var status = (context.TeamArenaStates ?? new TeamArenaSessionStateStore()).GetStatus(context.Connection);
        await SendTeamArenaInfoAsync(context, teamArenaStatistics, status, cancellationToken)
            .ConfigureAwait(false);
    }


    [PacketHandler(WorldPacketOpcodes.LegacyTeamArenaSignup)]
    private static async ValueTask HandleLegacyTeamArenaSignupAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTeamArenaSignupPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyTeamArenaSignup packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyTeamArenaSignup " +
                $"{FormatSession(context)} dialogType={packet.DialogType} dialogButton={packet.DialogButton} from {context.Connection.RemoteEndPoint}.")
            .ConfigureAwait(false);

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyTeamArenaSignup state handling {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var teamArenaStatistics = GetTeamArenaStatisticsForSession(context, session);
        await HandleTeamArenaSignupStateAsync(context, packet, session, teamArenaStatistics, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask HandleTeamArenaSignupOnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        TeamArenaStatistics statistics,
        CancellationToken cancellationToken)
    {
        if (!await HasTeamArenaLeaderAsync(context, session, "signup", logWhenMissing: true).ConfigureAwait(false))
        {
            return;
        }

        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (teamArenaStates.GetStatus(context.Connection) != LegacyTeamArenaInfoStatus.NotSignedUp)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped team arena signup state change {FormatSession(context)}; already signed up.")
                .ConfigureAwait(false);
            return;
        }

        if (statistics.ArenaPoints == 0)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped team arena signup state change {FormatSession(context)}; team arena points are zero.")
                .ConfigureAwait(false);
            return;
        }

        if (IsTeamArenaSignupBlockedMap(session.Character.MapId))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped team arena signup state change {FormatSession(context)}; map={session.Character.MapId} is blocked.")
                .ConfigureAwait(false);
            return;
        }

        var matched = teamArenaStates.RegisterWaiting(context.Connection, statistics.ArenaPoints, out var match);
        await SendTeamArenaInfoAsync(context, statistics, LegacyTeamArenaInfoStatus.WaitingForOpponent, cancellationToken)
            .ConfigureAwait(false);
        if (matched)
        {
            await SendTeamArenaMatchCountdownAsync(context, match, cancellationToken)
                .ConfigureAwait(false);
        }
    }


    private static async ValueTask HandleTeamArenaSignupOffAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        TeamArenaStatistics statistics,
        CancellationToken cancellationToken)
    {
        if (!await HasTeamArenaLeaderAsync(context, session, "quit", logWhenMissing: true).ConfigureAwait(false))
        {
            return;
        }

        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        teamArenaStates.SetStatus(context.Connection, LegacyTeamArenaInfoStatus.NotSignedUp);
        await SendTeamArenaInfoAsync(context, statistics, LegacyTeamArenaInfoStatus.NotSignedUp, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask HandleTeamArenaGiveUpAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        TeamArenaStatistics statistics,
        CancellationToken cancellationToken)
    {
        if (!await HasTeamArenaLeaderAsync(context, session, "give-up", logWhenMissing: true).ConfigureAwait(false))
        {
            return;
        }

        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (teamArenaStates.TryCompleteActiveMatchAsLoser(context.Connection, out var completed))
        {
            await SendTeamArenaInfoAsync(context, statistics, LegacyTeamArenaInfoStatus.WaitingInactive, cancellationToken)
                .ConfigureAwait(false);
            await SendTeamArenaGiveUpResultAsync(context, completed, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        teamArenaStates.SetStatus(context.Connection, LegacyTeamArenaInfoStatus.WaitingInactive);
        await SendTeamArenaInfoAsync(context, statistics, LegacyTeamArenaInfoStatus.WaitingInactive, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendTeamArenaGiveUpResultAsync(
        WorldPacketHandlerContext context,
        TeamArenaCompletedMatch completed,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeamLeaderSession(context, completed.Winner, out var winner) ||
            !TryGetTeamLeaderSession(context, completed.Loser, out var loser))
        {
            return;
        }

        var winnerStatistics = GetTeamArenaStatisticsForSession(context, winner.Connection, winner.Session);
        var loserStatistics = GetTeamArenaStatisticsForSession(context, loser.Connection, loser.Session);

        await winner.Connection.SendAsync(
                LegacyTeamArenaSignupResponsePacket.FromRequest(
                    new LegacyTeamArenaSignupPacket(
                        LegacyTeamArenaSignupDialogType.OpponentGaveUp,
                        LegacyTeamArenaSignupDialogButton.SignUp),
                    winner.Session.Character,
                    winnerStatistics).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await loser.Connection.SendAsync(
                LegacyTeamArenaInfoPacket.FromStatistics(loserStatistics, LegacyTeamArenaInfoStatus.NotSignedUp).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await winner.Connection.SendAsync(
                LegacyTeamArenaInfoPacket.FromStatistics(winnerStatistics, LegacyTeamArenaInfoStatus.NotSignedUp).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await SendTeamArenaDialogAsync(
                context,
                completed.Loser,
                loser.Session,
                loserStatistics,
                LegacyTeamArenaSignupDialogButton.Lose,
                cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaDialogAsync(
                context,
                completed.Winner,
                winner.Session,
                winnerStatistics,
                LegacyTeamArenaSignupDialogButton.Win,
                cancellationToken)
            .ConfigureAwait(false);
        await ExportTeamArenaCompletedWatchersAsync(context, completed, cancellationToken)
            .ConfigureAwait(false);
        await ReturnTeamArenaTeamAsync(context, completed.Loser, cancellationToken)
            .ConfigureAwait(false);
        await ReturnTeamArenaTeamAsync(context, completed.Winner, cancellationToken)
            .ConfigureAwait(false);
        await ApplyTeamArenaMatchStatisticsAsync(context, completed, cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFinalDialogAsync(
                context,
                completed.Loser,
                loser.Session,
                LegacyTeamArenaSignupDialogButton.SignUp,
                cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaFinalDialogAsync(
                context,
                completed.Winner,
                winner.Session,
                LegacyTeamArenaSignupDialogButton.Win,
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendTeamArenaInfoAsync(
        WorldPacketHandlerContext context,
        TeamArenaStatistics statistics,
        LegacyTeamArenaInfoStatus status,
        CancellationToken cancellationToken)
    {
        var packet = LegacyTeamArenaInfoPacket.FromStatistics(statistics, status);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyTeamArenaInfo {FormatSession(context)} status={status} todayRank={statistics.TodayRank} arenaPoints={statistics.ArenaPoints}.")
            .ConfigureAwait(false);
    }


    private static bool IsTeamArenaSignupBlockedMap(long mapId)
    {
        return mapId is 4020 or 3820 or 5040 or 2057 or 4000 or 6546 or 6525 or 8009 or
            6729 or 3825 or 4003 or 5342 or 4006 or 4008 or 4009 or 6000 or 6001 or
            6521 or 9988 or 6891 or 5599 or 8521 or 1017 or 1080 or 1081 or 2060 or
            6002 or 6003 or 601 or 700 or 1038 or 1764 or 1036 or 2068 or 3868;
    }


    private static TeamArenaStatistics GetTeamArenaStatisticsForSession(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        return GetTeamArenaStatisticsForSession(context, context.Connection, session);
    }


    private static TeamArenaStatistics GetTeamArenaStatisticsForSession(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        var statistics = (context.TeamArenaStatistics ?? TeamArenaStatisticsProvider.Empty)
            .GetForCharacter(session.Character.Uid);
        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        var purchasedPoints = teamArenaStates.GetPurchasedPoints(connection);
        var runtimeStatistics = teamArenaStates.GetRuntimeStatistics(connection);
        var arenaPoints = Math.Clamp(
            checked((long)statistics.ArenaPoints + purchasedPoints + runtimeStatistics.ArenaPointDelta),
            0,
            uint.MaxValue);

        return statistics with
        {
            ArenaPoints = checked((uint)arenaPoints),
            TotalWin = checked(statistics.TotalWin + runtimeStatistics.TotalWinBonus),
            TotalLose = checked(statistics.TotalLose + runtimeStatistics.TotalLoseBonus),
            TodayWin = checked(statistics.TodayWin + runtimeStatistics.TodayWinBonus),
            TodayBattles = checked(statistics.TodayBattles + runtimeStatistics.TodayBattlesBonus),
        };
    }


    private static async ValueTask HandleTeamArenaSignupStateAsync(
        WorldPacketHandlerContext context,
        LegacyTeamArenaSignupPacket packet,
        WorldSession session,
        TeamArenaStatistics statistics,
        CancellationToken cancellationToken)
    {
        switch (packet.DialogType)
        {
            case LegacyTeamArenaSignupDialogType.ArenaIconOn:
                await SendTeamArenaSignupResponseAsync(context, packet, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                await HandleTeamArenaSignupOnAsync(context, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyTeamArenaSignupDialogType.ArenaIconOff:
                await SendTeamArenaSignupResponseAsync(context, packet, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                await HandleTeamArenaSignupOffAsync(context, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyTeamArenaSignupDialogType.OpponentGaveUp
                when packet.DialogButton == LegacyTeamArenaSignupDialogButton.SignUp:
                await HandleTeamArenaSignupOffAsync(context, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyTeamArenaSignupDialogType.BuyPoints:
                await HandleTeamArenaBuyPointsAsync(context, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyTeamArenaSignupDialogType.ArenaGui
                when packet.DialogButton == LegacyTeamArenaSignupDialogButton.DoGiveUp:
                await HandleTeamArenaGiveUpAsync(context, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyTeamArenaSignupDialogType.ArenaGui
                when packet.DialogButton == LegacyTeamArenaSignupDialogButton.Accept:
                await HandleTeamArenaAcceptCountdownAsync(context, session, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyTeamArenaSignupDialogType.Continue
                when packet.DialogButton == LegacyTeamArenaSignupDialogButton.SignUp:
                await HandleTeamArenaSignupOnAsync(context, session, statistics, cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }


    private static async ValueTask SendTeamArenaSignupResponseAsync(
        WorldPacketHandlerContext context,
        LegacyTeamArenaSignupPacket packet,
        WorldSession session,
        TeamArenaStatistics statistics,
        CancellationToken cancellationToken)
    {
        var response = LegacyTeamArenaSignupResponsePacket.FromRequest(packet, session.Character, statistics);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyTeamArenaSignup response " +
                $"{FormatSession(context)} dialogType={packet.DialogType} dialogButton={packet.DialogButton} " +
                $"todayRank={statistics.TodayRank} arenaPoints={statistics.ArenaPoints}.")
            .ConfigureAwait(false);
    }
}
