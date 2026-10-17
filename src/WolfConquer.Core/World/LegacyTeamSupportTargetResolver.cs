using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WolfConquer.Tests")]

namespace WolfConquer.Core.World;

internal static class LegacyTeamSupportTargetResolver
{
    private const long LegacyAlienWorldMapId = 10_428;

    internal static IReadOnlyList<WorldActiveSession> ResolveActiveTeammates(
        WorldPacketHandlerContext context,
        WorldSession caster)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(caster);

        if (context.TeamStates is not { } teamStates ||
            context.Sessions is not { } sessions)
        {
            return Array.Empty<WorldActiveSession>();
        }

        var connections = teamStates.GetConnections(context.Connection);
        if (connections.Count == 0)
        {
            return Array.Empty<WorldActiveSession>();
        }

        List<WorldActiveSession>? teammates = null;
        foreach (var connection in connections)
        {
            if (ReferenceEquals(connection, context.Connection) ||
                !sessions.TryGet(connection, out var session) ||
                session is null ||
                session.Character.Uid == caster.Character.Uid ||
                session.Character.MapId != caster.Character.MapId ||
                session.Character.MapId == LegacyAlienWorldMapId)
            {
                continue;
            }

            (teammates ??= []).Add(new WorldActiveSession(connection, session));
        }

        return teammates is null ? Array.Empty<WorldActiveSession>() : teammates;
    }
}
