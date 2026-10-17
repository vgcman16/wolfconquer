using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyRemoveBuffersTeamDistance = 18;

    private static bool TryResolveLegacyRemoveBuffersSingleTarget(
        WorldPacketHandlerContext context,
        WorldSession casterSession,
        uint targetUid,
        out WorldActiveSession target)
    {
        if (targetUid == casterSession.Character.Uid)
        {
            target = new WorldActiveSession(context.Connection, casterSession);
            return true;
        }

        return TryGetVisibleSessionByUid(context, casterSession, targetUid, out target);
    }

    private static IReadOnlyList<WorldActiveSession> SnapshotLegacyRemoveBuffersTeamMembers(
        WorldPacketHandlerContext context,
        WorldSession casterSession)
    {
        if (context.TeamStates is not { } teamStates ||
            context.Sessions is not { } sessions ||
            !teamStates.HasTeam(context.Connection))
        {
            return [];
        }

        var targets = new List<WorldActiveSession>();
        foreach (var connection in teamStates.GetConnections(context.Connection))
        {
            WorldSession? memberSession;
            if (ReferenceEquals(connection, context.Connection))
            {
                memberSession = casterSession;
            }
            else if (!sessions.TryGet(connection, out memberSession) || memberSession is null)
            {
                continue;
            }

            if (!IsLegacyRemoveBuffersTeamMemberInRange(
                    casterSession.Character,
                    memberSession.Character))
            {
                continue;
            }

            targets.Add(new WorldActiveSession(connection, memberSession));
        }

        return targets;
    }

    internal static bool IsLegacyRemoveBuffersTeamMemberInRange(
        CharacterSnapshot caster,
        CharacterSnapshot member)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(member);

        // Matching Team.cs includes the owner and RemoveBuffers.cs has no map gate.
        return LegacyGetDistance(caster.X, caster.Y, member.X, member.Y) <
            LegacyRemoveBuffersTeamDistance;
    }
}
