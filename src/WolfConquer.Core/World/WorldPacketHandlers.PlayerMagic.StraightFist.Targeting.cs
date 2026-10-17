using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyStraightFistMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        byte lineWidth)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (IsLegacyGuardMonsterFamily(candidate.Monster, candidate.MonsterFamily) ||
                LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) >= LegacyStraightFistRange ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    LegacyStraightFistRange,
                    lineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyStraightFistPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        byte lineWidth)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y) >= LegacyStraightFistRange ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    LegacyStraightFistRange,
                    lineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyStraightFistSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        byte lineWidth)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) >= LegacyStraightFistRange ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    LegacyStraightFistRange,
                    lineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static WorldSession ApplyLegacyStraightFistCounterMissShoot(
        WorldPacketHandlerContext context,
        WorldSession session,
        out int counterMissShoots)
    {
        counterMissShoots = 0;
        if (!LegacyCounterHitMaps.Contains(session.Character.MapId))
        {
            return session;
        }

        counterMissShoots = 1;
        var current = ReadAttributeUInt64(
            session.Character.Attributes,
            LegacyStraightFistCounterMissShootAttribute);
        var updatedMissShoot = current == ulong.MaxValue ? ulong.MaxValue : current + 1;
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(
                session.Character,
                LegacyStraightFistCounterMissShootAttribute,
                updatedMissShoot),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }
}
