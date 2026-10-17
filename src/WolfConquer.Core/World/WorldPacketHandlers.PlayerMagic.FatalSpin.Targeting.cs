using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyFatalSpinMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell damageSpell,
        byte lineRange,
        byte lineWidth)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) >= damageSpell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    lineRange,
                    lineWidth) ||
                !CanLegacyPlayerAttackMonster(session.Character, candidate.Monster, candidate.MonsterFamily, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyFatalSpinPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell damageSpell,
        byte lineRange,
        byte lineWidth,
        long distanceLimit,
        bool distanceInclusive)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, damageSpell))
        {
            var character = candidate.ActiveSession.Session.Character;
            var distance = LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                character.X,
                character.Y);
            if (!IsWithinLegacyFatalSpinPlayerDistance(distance, distanceLimit, distanceInclusive) ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y,
                    lineRange,
                    lineWidth))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyFatalSpinSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell damageSpell,
        byte lineRange,
        byte lineWidth)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) >= damageSpell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    lineRange,
                    lineWidth) ||
                !CanLegacySobNpcMagicDamageTarget(context, session.Character, candidate.SobNpc, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static bool IsWithinLegacyFatalSpinPlayerDistance(
        long distance,
        long distanceLimit,
        bool inclusive)
    {
        return inclusive ? distance <= distanceLimit : distance < distanceLimit;
    }

    private static LegacyFatalSpinFollowUpAnchor? SelectLegacyFatalSpinFollowUpAnchor(
        IReadOnlyList<LegacyPlayerMonsterMagicTarget> monsterTargets,
        IReadOnlyList<LegacyPlayerMagicTarget> playerTargets)
    {
        LegacyFatalSpinFollowUpAnchor? anchor = null;
        foreach (var target in monsterTargets)
        {
            anchor = new LegacyFatalSpinFollowUpAnchor(target.Monster.X, target.Monster.Y);
        }

        // FatalSpin.cs iterates players after monsters, so the last player always wins the anchor.
        foreach (var target in playerTargets)
        {
            var character = target.ActiveSession.Session.Character;
            anchor = new LegacyFatalSpinFollowUpAnchor(
                CheckedUInt16(character.X, nameof(character.X)),
                CheckedUInt16(character.Y, nameof(character.Y)));
        }

        return anchor;
    }
}
