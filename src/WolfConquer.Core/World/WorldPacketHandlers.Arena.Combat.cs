using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleTeamArenaCombatDamageAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        var query = packet.Query;
        if (query.Damage <= 0 ||
            !IsArenaCombatAttackType(query.AttackType) ||
            context.Sessions?.TryGet(context.Connection, out var session) != true ||
            session is null ||
            session.Character.MapId != LegacyTeamArenaMapId)
        {
            return;
        }

        var attackerUid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        if (!TryFindActiveTeamArenaMatchByMemberUid(context, attackerUid, out var match))
        {
            return;
        }

        var attackerOnFirstTeam = TeamArenaTeamContainsUid(context, match.First, attackerUid);
        var targetOnFirstTeam = TeamArenaTeamContainsUid(context, match.First, query.OpponentUid);
        var targetOnSecondTeam = TeamArenaTeamContainsUid(context, match.Second, query.OpponentUid);
        if ((attackerOnFirstTeam && !targetOnSecondTeam) ||
            (!attackerOnFirstTeam && !targetOnFirstTeam) ||
            !TryGetSessionByUid(context, query.OpponentUid, out var target) ||
            target.Session.Character.MapId != LegacyTeamArenaMapId)
        {
            return;
        }

        var targetHitpoints = target.Session.Character.MinHitPoints;
        var teamArenaStates = context.TeamArenaStates ?? new TeamArenaSessionStateStore();
        if (targetHitpoints <= query.Damage)
        {
            var defeatedCharacter = target.Session.Character with { MinHitPoints = 0 };
            context.Sessions?.Set(target.Connection, target.Session with { Character = defeatedCharacter });
            var loserTeam = attackerOnFirstTeam ? match.Second : match.First;
            if (teamArenaStates.TryCompleteActiveMatchAsLoser(loserTeam, out var completed))
            {
                await SendTeamArenaCombatEndResultAsync(context, completed, cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        if (!teamArenaStates.TryAddDamage(match, attackerOnFirstTeam, (uint)query.Damage, out var update))
        {
            return;
        }

        var damagedCharacter = target.Session.Character with
        {
            MinHitPoints = targetHitpoints - query.Damage,
        };
        context.Sessions?.Set(target.Connection, target.Session with { Character = damagedCharacter });
        await SendTeamArenaMatchScoreAsync(context, update.Match, cancellationToken).ConfigureAwait(false);
    }


    private static async ValueTask HandleArenaCombatDamageAsync(
        WorldPacketHandlerContext context,
        MsgAttackPacket packet,
        CancellationToken cancellationToken)
    {
        var query = packet.Query;
        if (query.Damage <= 0 ||
            !IsArenaCombatAttackType(query.AttackType) ||
            context.Sessions?.TryGet(context.Connection, out var session) != true ||
            session is null ||
            session.Character.MapId != LegacyArenaMapId ||
            !TryGetSessionByUid(context, query.OpponentUid, out var target) ||
            target.Session.Character.MapId != LegacyArenaMapId)
        {
            return;
        }

        var arenaStates = context.ArenaStates ?? new ArenaSessionStateStore();
        var matchFound = false;
        foreach (var match in arenaStates.GetActiveMatches())
        {
            if (!((ReferenceEquals(match.First, context.Connection) && ReferenceEquals(match.Second, target.Connection)) ||
                (ReferenceEquals(match.Second, context.Connection) && ReferenceEquals(match.First, target.Connection))))
            {
                continue;
            }

            matchFound = true;
            break;
        }

        if (!matchFound)
        {
            return;
        }

        var targetHitpoints = target.Session.Character.MinHitPoints;
        if (targetHitpoints <= query.Damage)
        {
            var defeatedCharacter = target.Session.Character with { MinHitPoints = 0 };
            context.Sessions?.Set(target.Connection, target.Session with { Character = defeatedCharacter });
            if (arenaStates.TryCompleteActiveMatchAsLoser(target.Connection, out var completed))
            {
                await SendArenaCombatEndResultAsync(context, completed, cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        if (!arenaStates.TryAddDamage(context.Connection, target.Connection, (uint)query.Damage, out var update))
        {
            return;
        }

        var damagedCharacter = target.Session.Character with
        {
            MinHitPoints = targetHitpoints - query.Damage,
        };
        context.Sessions?.Set(target.Connection, target.Session with { Character = damagedCharacter });
        await SendArenaMatchScoreAsync(context, update.Match, cancellationToken).ConfigureAwait(false);
    }


    private static bool IsArenaCombatAttackType(MsgAttackType attackType)
    {
        return attackType is MsgAttackType.Physical
            or MsgAttackType.Magic
            or MsgAttackType.Archer
            or MsgAttackType.Dash
            or MsgAttackType.FatalStrike
            or MsgAttackType.InMoveSpell
            or MsgAttackType.BlueDamage
            or MsgAttackType.BackFire;
    }


    private static async ValueTask SendTeamArenaCombatEndResultAsync(
        WorldPacketHandlerContext context,
        TeamArenaCompletedMatch completed,
        CancellationToken cancellationToken)
    {
        if (!TryGetTeamLeaderSession(context, completed.Winner, out var winner) ||
            !TryGetTeamLeaderSession(context, completed.Loser, out var loser))
        {
            return;
        }

        await SendTeamArenaDialogAsync(
                context,
                completed.Loser,
                loser.Session,
                GetTeamArenaStatisticsForSession(context, loser.Connection, loser.Session),
                LegacyTeamArenaSignupDialogButton.Lose,
                cancellationToken)
            .ConfigureAwait(false);
        await SendTeamArenaDialogAsync(
                context,
                completed.Winner,
                winner.Session,
                GetTeamArenaStatisticsForSession(context, winner.Connection, winner.Session),
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
}
