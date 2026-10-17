using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

[assembly: InternalsVisibleTo("WolfConquer.Tests")]

namespace WolfConquer.Core.World;

internal sealed record LegacyMagicSupportPlayerTarget(
    WorldActiveSession ActiveSession,
    bool IsTeamMember);

internal static class LegacyMagicSupportTargetResolver
{
    private const long LegacyAlienWorldMapId = 10_428;
    private const long LegacyViewThreshold = 18;

    internal static bool TryResolveVisiblePlayerTarget(
        WorldPacketHandlerContext context,
        WorldSession caster,
        uint targetUid,
        out LegacyMagicSupportPlayerTarget target)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(caster);

        if (targetUid == 0 ||
            targetUid == caster.Character.Uid ||
            context.Sessions is not { } sessions ||
            !TryGetSessionByUid(sessions, targetUid, out var activeSession) ||
            ReferenceEquals(activeSession.Connection, context.Connection) ||
            !CanSeePlayer(caster.Character, activeSession.Session.Character))
        {
            target = default!;
            return false;
        }

        target = new LegacyMagicSupportPlayerTarget(
            activeSession,
            IsCurrentTeammate(context, activeSession.Connection));
        return true;
    }

    private static bool TryGetSessionByUid(
        WorldSessionStore sessions,
        uint uid,
        out WorldActiveSession activeSession)
    {
        foreach (var active in sessions.Snapshot())
        {
            if (active.Session.Character.Uid == uid)
            {
                activeSession = active;
                return true;
            }
        }

        activeSession = default!;
        return false;
    }

    private static bool CanSeePlayer(CharacterSnapshot character, CharacterSnapshot other)
    {
        return character.MapId == other.MapId &&
            character.MapId != LegacyAlienWorldMapId &&
            ReadCharacterDynamicId(character) == ReadCharacterDynamicId(other) &&
            GetChebyshevDistance(character.X, character.Y, other.X, other.Y) <= LegacyViewThreshold;
    }

    private static uint ReadCharacterDynamicId(CharacterSnapshot character)
    {
        foreach (var key in new[] { "DynamicID", "DynamicId" })
        {
            if (character.Attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static bool IsCurrentTeammate(
        WorldPacketHandlerContext context,
        ConquerTcpConnection targetConnection)
    {
        if (context.TeamStates is not { } teamStates)
        {
            return false;
        }

        foreach (var connection in teamStates.GetConnections(context.Connection))
        {
            if (ReferenceEquals(connection, targetConnection))
            {
                return true;
            }
        }

        return false;
    }

    private static long GetChebyshevDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }
}
