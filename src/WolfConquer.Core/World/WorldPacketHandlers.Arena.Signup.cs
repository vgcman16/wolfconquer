using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeArenaCurrentHonorAttribute = "ArenaCurrentHonor";

    [PacketHandler(WorldPacketOpcodes.MsgArenaInfo)]
    private static async ValueTask HandleMsgArenaInfoAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (frame.Header.Opcode != WorldPacketOpcodes.MsgArenaInfo)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgArenaInfo packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgArenaInfo {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var arenaStatistics = GetArenaStatisticsForSession(context, session);
        var status = context.ArenaStates?.GetStatus(context.Connection) ?? MsgArenaInfoStatus.NotSignedUp;
        await SendArenaInfoAsync(context, arenaStatistics, status, cancellationToken).ConfigureAwait(false);
    }


    [PacketHandler(WorldPacketOpcodes.MsgArenaSignup)]
    private static async ValueTask HandleMsgArenaSignupAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!MsgArenaSignupPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed MsgArenaSignup packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                $"WorldServer handled MsgArenaSignup {FormatSession(context)} dialogType={packet.DialogType} dialogButton={packet.DialogButton} from {context.Connection.RemoteEndPoint}.")
            .ConfigureAwait(false);

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgArenaSignup state handling {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var arenaStatistics = GetArenaStatisticsForSession(context, session);
        await HandleArenaSignupStateAsync(context, packet, session, arenaStatistics, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleArenaSignupOnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ArenaStatistics arenaStatistics,
        CancellationToken cancellationToken)
    {
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (arenaStates.GetStatus(context.Connection) != MsgArenaInfoStatus.NotSignedUp)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped arena signup state change {FormatSession(context)}; already signed up.")
                .ConfigureAwait(false);
            return;
        }

        if (arenaStatistics.ArenaPoints == 0)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped arena signup state change {FormatSession(context)}; arena points are zero.")
                .ConfigureAwait(false);
            return;
        }

        if (IsArenaSignupBlockedMap(session.Character.MapId))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped arena signup state change {FormatSession(context)}; map={session.Character.MapId} is blocked.")
                .ConfigureAwait(false);
            return;
        }

        var matched = arenaStates.RegisterWaiting(context.Connection, arenaStatistics.ArenaPoints, out var match);
        await SendArenaInfoAsync(context, arenaStatistics, MsgArenaInfoStatus.WaitingForOpponent, cancellationToken)
            .ConfigureAwait(false);
        if (matched)
        {
            await SendArenaMatchCountdownAsync(context, match, cancellationToken)
                .ConfigureAwait(false);
        }
    }


    private static async ValueTask HandleArenaSignupOffAsync(
        WorldPacketHandlerContext context,
        ArenaStatistics arenaStatistics,
        CancellationToken cancellationToken)
    {
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        arenaStates.SetStatus(context.Connection, MsgArenaInfoStatus.NotSignedUp);
        await SendArenaInfoAsync(context, arenaStatistics, MsgArenaInfoStatus.NotSignedUp, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendArenaInfoAsync(
        WorldPacketHandlerContext context,
        ArenaStatistics arenaStatistics,
        MsgArenaInfoStatus status,
        CancellationToken cancellationToken)
    {
        var packet = MsgArenaInfoPacket.FromStatistics(arenaStatistics, status);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent MsgArenaInfo {FormatSession(context)} status={status} todayRank={arenaStatistics.TodayRank} arenaPoints={arenaStatistics.ArenaPoints}.")
            .ConfigureAwait(false);
    }

    private static bool IsArenaSignupBlockedMap(long mapId)
    {
        return mapId is 10397 or 10398 or 10399 or 10401 or 10402 or 10403 or 10404 or
            10437 or 10438 or 10439 or 10440 or 10441 or 10442 or 10770 or 10298 or
            10762 or 10761 or 10300 or 10760 or 10593 or 10763 or 10299 or 10079 or
            3872 or 10759 or 10301;
    }

    private static ArenaStatistics GetArenaStatisticsForSession(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        return GetArenaStatisticsForSession(context, context.Connection, session);
    }


    private static ArenaStatistics GetArenaStatisticsForSession(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        var statistics = (context.ArenaStatistics ?? ArenaStatisticsProvider.Empty)
            .GetForCharacter(session.Character.Uid);
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        var runtimeStatistics = arenaStates.GetRuntimeStatistics(connection);
        var arenaPoints = Math.Clamp(
            checked((long)statistics.ArenaPoints + runtimeStatistics.ArenaPointDelta),
            0,
            uint.MaxValue);

        statistics = statistics with
        {
            ArenaPoints = checked((uint)arenaPoints),
            TotalWin = checked(statistics.TotalWin + runtimeStatistics.TotalWinBonus),
            TotalLose = checked(statistics.TotalLose + runtimeStatistics.TotalLoseBonus),
            TodayWin = checked(statistics.TodayWin + runtimeStatistics.TodayWinBonus),
            TodayBattles = checked(statistics.TodayBattles + runtimeStatistics.TodayBattlesBonus),
        };
        statistics = statistics with
        {
            TodayRank = GetRuntimeArenaTodayRank(context, arenaStates, connection, session, statistics),
        };

        if (session.Character.Attributes.TryGetValue(RuntimeArenaCurrentHonorAttribute, out var currentHonor) &&
            uint.TryParse(currentHonor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return statistics with { CurrentHonor = parsed };
        }

        return statistics;
    }


    private static uint GetRuntimeArenaTodayRank(
        WorldPacketHandlerContext context,
        ArenaSessionStateStore arenaStates,
        ConquerTcpConnection connection,
        WorldSession session,
        ArenaStatistics statistics)
    {
        if (statistics.TodayRank == 0 ||
            context.ArenaStatistics is not { Records.Count: > 0 } arenaStatistics ||
            session.Character.Uid is < int.MinValue or > int.MaxValue)
        {
            return statistics.TodayRank;
        }

        var playerId = checked((int)session.Character.Uid);
        var runtimeDeltas = new Dictionary<int, long>();
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (active.Session.Character.Uid is < int.MinValue or > int.MaxValue)
                {
                    continue;
                }

                var activePlayerId = checked((int)active.Session.Character.Uid);
                var runtime = arenaStates.GetRuntimeStatistics(active.Connection);
                if (runtime.ArenaPointDelta != 0)
                {
                    runtimeDeltas[activePlayerId] = runtime.ArenaPointDelta;
                }
            }
        }

        runtimeDeltas[playerId] = arenaStates.GetRuntimeStatistics(connection).ArenaPointDelta;

        uint rank = 1;
        foreach (var record in arenaStatistics.Records
            .OrderByDescending(record => GetAdjustedArenaPoints(record, runtimeDeltas))
            .Take(1000))
        {
            if (record.PlayerId == playerId)
            {
                return rank;
            }

            rank++;
        }

        return 0;
    }


    private static long GetAdjustedArenaPoints(
        ArenaRecord record,
        IReadOnlyDictionary<int, long> runtimeDeltas)
    {
        var points = record.Points;
        if (runtimeDeltas.TryGetValue(record.PlayerId, out var delta))
        {
            return Math.Clamp(
                checked((long)points + delta),
                0L,
                uint.MaxValue);
        }

        return points;
    }

    private static CharacterSnapshot WithArenaCurrentHonor(CharacterSnapshot character, uint currentHonor)
    {
        return WithCharacterAttribute(character, RuntimeArenaCurrentHonorAttribute, currentHonor);
    }


    private static async ValueTask HandleArenaSignupStateAsync(
        WorldPacketHandlerContext context,
        MsgArenaSignupPacket packet,
        WorldSession session,
        ArenaStatistics arenaStatistics,
        CancellationToken cancellationToken)
    {
        switch (packet.DialogType)
        {
            case MsgArenaSignupDialogType.ArenaIconOn:
                await SendArenaSignupResponseAsync(context, packet, session, arenaStatistics, cancellationToken)
                    .ConfigureAwait(false);
                await HandleArenaSignupOnAsync(context, session, arenaStatistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgArenaSignupDialogType.ArenaIconOff:
                await SendArenaSignupResponseAsync(context, packet, session, arenaStatistics, cancellationToken)
                    .ConfigureAwait(false);
                await HandleArenaSignupOffAsync(context, arenaStatistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgArenaSignupDialogType.OpponentGaveUp
                when packet.DialogButton == MsgArenaSignupDialogButton.SignUp:
                await HandleArenaSignupOffAsync(context, arenaStatistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgArenaSignupDialogType.ArenaGui
                when packet.DialogButton == MsgArenaSignupDialogButton.DoGiveUp:
                await HandleArenaGiveUpAsync(context, arenaStatistics, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgArenaSignupDialogType.ArenaGui
                when packet.DialogButton == MsgArenaSignupDialogButton.Accept:
                await HandleArenaAcceptCountdownAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case MsgArenaSignupDialogType.Continue
                when packet.DialogButton == MsgArenaSignupDialogButton.SignUp:
                await HandleArenaSignupOnAsync(context, session, arenaStatistics, cancellationToken)
                    .ConfigureAwait(false);
                break;
        }
    }

    private static async ValueTask SendArenaSignupResponseAsync(
        WorldPacketHandlerContext context,
        MsgArenaSignupPacket packet,
        WorldSession session,
        ArenaStatistics arenaStatistics,
        CancellationToken cancellationToken)
    {
        var response = MsgArenaSignupResponsePacket.FromRequest(packet, session.Character, arenaStatistics);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent MsgArenaSignup response " +
                $"{FormatSession(context)} dialogType={packet.DialogType} dialogButton={packet.DialogButton} " +
                $"todayRank={arenaStatistics.TodayRank} arenaPoints={arenaStatistics.ArenaPoints}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleArenaGiveUpAsync(
        WorldPacketHandlerContext context,
        ArenaStatistics arenaStatistics,
        CancellationToken cancellationToken)
    {
        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        if (arenaStates.TryDeclinePendingMatch(context.Connection))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer declined pending Arena countdown {FormatSession(context)} without active give-up flow.")
                .ConfigureAwait(false);
            return;
        }

        if (arenaStates.TryCompleteActiveMatchAsLoser(context.Connection, out var completed))
        {
            await SendArenaInfoAsync(context, arenaStatistics, MsgArenaInfoStatus.WaitingInactive, cancellationToken)
                .ConfigureAwait(false);
            await SendArenaGiveUpResultAsync(context, completed, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        arenaStates.SetStatus(context.Connection, MsgArenaInfoStatus.WaitingInactive);
        await SendArenaInfoAsync(context, arenaStatistics, MsgArenaInfoStatus.WaitingInactive, cancellationToken)
            .ConfigureAwait(false);
    }


    private static async ValueTask SendArenaGiveUpResultAsync(
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

        await completed.Winner.SendAsync(
                MsgArenaSignupResponsePacket.FromRequest(
                    new MsgArenaSignupPacket(
                        MsgArenaSignupDialogType.OpponentGaveUp,
                        MsgArenaSignupDialogButton.SignUp),
                    loserSession.Character,
                    loserStatistics).ToFrameBytes(),
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
}
