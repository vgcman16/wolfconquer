using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendArenaPendingDeclineResultAsync(
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

        var winnerStatistics = GetArenaStatisticsForSession(context, completed.Winner, winnerSession);
        var loserStatistics = GetArenaStatisticsForSession(context, completed.Loser, loserSession);

        await completed.Winner.SendAsync(
                MsgArenaInfoPacket.FromStatistics(winnerStatistics, MsgArenaInfoStatus.NotSignedUp).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await completed.Loser.SendAsync(
                MsgArenaInfoPacket.FromStatistics(loserStatistics, MsgArenaInfoStatus.NotSignedUp).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await ApplyArenaMatchStatisticsAsync(context, completed, cancellationToken)
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

        winnerStatistics = GetArenaStatisticsForSession(context, completed.Winner, winnerSession);
        loserStatistics = GetArenaStatisticsForSession(context, completed.Loser, loserSession);

        await completed.Loser.SendAsync(
                MsgArenaSignupResponsePacket.FromRequest(
                    new MsgArenaSignupPacket(
                        MsgArenaSignupDialogType.Dialog2,
                        MsgArenaSignupDialogButton.SignUp),
                    loserSession.Character,
                    loserStatistics).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await completed.Winner.SendAsync(
                MsgArenaSignupResponsePacket.FromRequest(
                    new MsgArenaSignupPacket(
                        MsgArenaSignupDialogType.Dialog2,
                        MsgArenaSignupDialogButton.Win),
                    winnerSession.Character,
                    winnerStatistics).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await completed.Winner.SendAsync(
                MsgArenaInfoPacket.FromStatistics(winnerStatistics, MsgArenaInfoStatus.NotSignedUp).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await completed.Loser.SendAsync(
                MsgArenaInfoPacket.FromStatistics(loserStatistics, MsgArenaInfoStatus.NotSignedUp).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendArenaCombatEndResultAsync(
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

        var winnerStatistics = GetArenaStatisticsForSession(context, winnerSession);
        var loserStatistics = GetArenaStatisticsForSession(context, loserSession);

        await SendArenaDialogAsync(
                completed.Loser,
                loserSession,
                loserStatistics,
                MsgArenaSignupDialogButton.Lose,
                cancellationToken)
            .ConfigureAwait(false);
        await SendArenaDialogAsync(
                completed.Winner,
                winnerSession,
                winnerStatistics,
                MsgArenaSignupDialogButton.Win,
                cancellationToken)
            .ConfigureAwait(false);
        await ExportArenaCompletedWatchersAsync(context, completed, cancellationToken)
            .ConfigureAwait(false);
        await ReturnArenaPlayerAsync(context, completed.Loser, cancellationToken)
            .ConfigureAwait(false);
        await ReturnArenaPlayerAsync(context, completed.Winner, cancellationToken)
            .ConfigureAwait(false);
        ApplyArenaEndWinnerHeroReward(context, completed.Winner);
        await ApplyArenaMatchStatisticsAsync(context, completed, cancellationToken)
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

        winnerStatistics = GetArenaStatisticsForSession(context, completed.Winner, winnerSession);
        loserStatistics = GetArenaStatisticsForSession(context, completed.Loser, loserSession);

        await SendArenaFinalDialogAsync(
                completed.Loser,
                loserSession,
                loserStatistics,
                MsgArenaSignupDialogButton.SignUp,
                cancellationToken)
            .ConfigureAwait(false);
        await SendArenaFinalDialogAsync(
                completed.Winner,
                winnerSession,
                winnerStatistics,
                MsgArenaSignupDialogButton.Win,
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendArenaDialogAsync(
        ConquerTcpConnection connection,
        WorldSession session,
        ArenaStatistics statistics,
        MsgArenaSignupDialogButton result,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(
                MsgArenaSignupResponsePacket.FromRequest(
                    new MsgArenaSignupPacket(
                        MsgArenaSignupDialogType.Dialog,
                        result),
                    session.Character,
                    statistics).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendArenaFinalDialogAsync(
        ConquerTcpConnection connection,
        WorldSession session,
        ArenaStatistics statistics,
        MsgArenaSignupDialogButton result,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(
                MsgArenaSignupResponsePacket.FromRequest(
                    new MsgArenaSignupPacket(
                        MsgArenaSignupDialogType.Dialog2,
                        result),
                    session.Character,
                    statistics).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await connection.SendAsync(
                MsgArenaInfoPacket.FromStatistics(statistics, MsgArenaInfoStatus.NotSignedUp).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static void ApplyArenaEndWinnerHeroReward(
        WorldPacketHandlerContext context,
        ConquerTcpConnection winner)
    {
        if (context.Sessions?.TryGet(winner, out var winnerSession) == true &&
            winnerSession is not null)
        {
            context.Sessions.Set(
                winner,
                AddLegacyHeroRewardGoal(winnerSession, LegacyArenaEndHeroRewardGoalId));
        }
    }
}
