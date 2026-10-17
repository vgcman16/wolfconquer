using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendLegacyPlayerReceiveQualifierScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        if (result.Killed ||
            result.DamageApplied == 0 ||
            attacker.Character.MapId != LegacyArenaMapId)
        {
            return;
        }

        await SendLegacyPlayerReceiveRegularArenaQualifierScoreAsync(
                context,
                attacker,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerReceiveTeamArenaQualifierScoreAsync(
                context,
                attacker,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerReceiveTeamPkQualifierScoreAsync(
                context,
                attacker,
                result,
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyPlayerReceiveElitePkQualifierScoreAsync(
                context,
                attacker,
                result,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPlayerReceiveRegularArenaQualifierScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        var arenaStates = context.ArenaStates;
        if (arenaStates is null ||
            !arenaStates.TryAddDamage(
                context.Connection,
                result.ActiveSession.Connection,
                result.DamageApplied,
                out var update))
        {
            return;
        }

        await SendArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy player receive qualifier arena score " +
                $"{FormatSession(context with { Session = attacker })} " +
                $"targetUid={result.UpdatedSession.Character.Uid} damage={result.DamageApplied}; " +
                "source=GameClient.UpdateQualifier ArenaMatch.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPlayerReceiveTeamArenaQualifierScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        var attackerUid = CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid));
        if (!TryFindActiveTeamArenaMatchByMemberUid(context, attackerUid, out var match))
        {
            return;
        }

        var targetUid = CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid));
        var attackerOnFirstTeam = TeamArenaTeamContainsUid(context, match.First, attackerUid);
        var targetOnFirstTeam = TeamArenaTeamContainsUid(context, match.First, targetUid);
        var targetOnSecondTeam = TeamArenaTeamContainsUid(context, match.Second, targetUid);
        if ((attackerOnFirstTeam && !targetOnSecondTeam) ||
            (!attackerOnFirstTeam && !targetOnFirstTeam))
        {
            return;
        }

        var teamArenaStates = context.TeamArenaStates;
        if (teamArenaStates is null ||
            !teamArenaStates.TryAddDamage(match, attackerOnFirstTeam, result.DamageApplied, out var update))
        {
            return;
        }

        await SendTeamArenaMatchScoreAsync(context, update.Match, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy player receive team qualifier score " +
                $"{FormatSession(context with { Session = attacker })} " +
                $"targetUid={targetUid} damage={result.DamageApplied}; " +
                "source=GameClient.UpdateQualifier Team.TeamArenaMatch.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPlayerReceiveTeamPkQualifierScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        var attackerUid = CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid));
        if (!TryFindActiveLegacyTeamPkMatchByMemberUid(context, attackerUid, out var match))
        {
            return;
        }

        var targetUid = CheckedUInt32(result.UpdatedSession.Character.Uid, nameof(result.UpdatedSession.Character.Uid));
        var attackerOnFirstTeam = TeamArenaTeamContainsUid(context, match.First, attackerUid);
        var targetOnFirstTeam = TeamArenaTeamContainsUid(context, match.First, targetUid);
        var targetOnSecondTeam = TeamArenaTeamContainsUid(context, match.Second, targetUid);
        if ((attackerOnFirstTeam && !targetOnSecondTeam) ||
            (!attackerOnFirstTeam && !targetOnFirstTeam))
        {
            return;
        }

        var teamPkStates = context.TeamPkStates;
        if (teamPkStates is null ||
            !teamPkStates.TryAddDamage(match, attackerOnFirstTeam, result.DamageApplied, out var update))
        {
            return;
        }

        await SendLegacyTeamPkMatchStatsAsync(context, update, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy player receive team pk score " +
                $"{FormatSession(context with { Session = attacker })} " +
                $"targetUid={targetUid} damage={result.DamageApplied}; " +
                "source=GameClient.UpdateQualifier Team.PkMatch.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPlayerReceiveElitePkQualifierScoreAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        var elitePkStates = context.ElitePkStates;
        if (elitePkStates is null ||
            !elitePkStates.TryAddDamage(
                context.Connection,
                result.ActiveSession.Connection,
                result.DamageApplied,
                out var update))
        {
            return;
        }

        await SendLegacyElitePkMatchStatsAsync(context, update, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy player receive elite pk score " +
                $"{FormatSession(context with { Session = attacker })} " +
                $"targetUid={result.UpdatedSession.Character.Uid} damage={result.DamageApplied}; " +
                "source=GameClient.UpdateQualifier ElitePKStats/ElitePkMatch.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyElitePkMatchStatsAsync(
        WorldPacketHandlerContext context,
        LegacyElitePkScoreUpdate update,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions ||
            !sessions.TryGet(update.Match.First, out var firstSession) ||
            firstSession is null ||
            !sessions.TryGet(update.Match.Second, out var secondSession) ||
            secondSession is null)
        {
            return;
        }

        var packet = LegacyElitePkMatchStatsPacket.Create(
            CheckedUInt32(firstSession.Character.Uid, nameof(firstSession.Character.Uid)),
            firstSession.Character.Name,
            ReadLegacyTournamentServerId(firstSession.Character),
            update.FirstPoints,
            CheckedUInt32(secondSession.Character.Uid, nameof(secondSession.Character.Uid)),
            secondSession.Character.Name,
            ReadLegacyTournamentServerId(secondSession.Character),
            update.SecondPoints);
        var frame = packet.ToFrameBytes();
        if (IsLegacyTournamentScoreRecipient(firstSession, update.DynamicId))
        {
            await update.Match.First.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        if (IsLegacyTournamentScoreRecipient(secondSession, update.DynamicId))
        {
            await update.Match.Second.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyTeamPkMatchStatsAsync(
        WorldPacketHandlerContext context,
        LegacyTeamPkScoreUpdate update,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions ||
            context.TeamStates is not { } teamStates ||
            !sessions.TryGet(update.Match.First, out var firstLeaderSession) ||
            firstLeaderSession is null ||
            !sessions.TryGet(update.Match.Second, out var secondLeaderSession) ||
            secondLeaderSession is null)
        {
            return;
        }

        var packet = LegacyTeamPkMatchStatsPacket.Create(
            CheckedUInt32(firstLeaderSession.Character.Uid, nameof(firstLeaderSession.Character.Uid)),
            teamStates.GetTeamUid(update.Match.First),
            firstLeaderSession.Character.Name,
            teamStates.GetTeamName(update.Match.First),
            update.FirstPoints,
            CheckedUInt32(secondLeaderSession.Character.Uid, nameof(secondLeaderSession.Character.Uid)),
            teamStates.GetTeamUid(update.Match.Second),
            secondLeaderSession.Character.Name,
            teamStates.GetTeamName(update.Match.Second),
            update.SecondPoints);
        var frame = packet.ToFrameBytes();

        foreach (var connection in teamStates.GetConnections(update.Match.First))
        {
            if (sessions.TryGet(connection, out var session) &&
                session is not null &&
                IsLegacyTournamentScoreRecipient(session, update.DynamicId))
            {
                await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var connection in teamStates.GetConnections(update.Match.Second))
        {
            if (sessions.TryGet(connection, out var session) &&
                session is not null &&
                IsLegacyTournamentScoreRecipient(session, update.DynamicId))
            {
                await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool TryFindActiveLegacyTeamPkMatchByMemberUid(
        WorldPacketHandlerContext context,
        uint uid,
        out LegacyTeamPkMatchPair match)
    {
        foreach (var activeMatch in (context.TeamPkStates ?? new LegacyTeamPkSessionStateStore()).GetActiveMatches())
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

    private static bool IsLegacyTournamentScoreRecipient(WorldSession session, uint dynamicId)
    {
        return session.Character.MapId == LegacyArenaMapId &&
            ReadCharacterDynamicId(session.Character) == dynamicId;
    }

    private static uint ReadLegacyTournamentServerId(CharacterSnapshot character)
    {
        foreach (var key in new[] { "ServerID", "ServerId" })
        {
            if (character.Attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}
