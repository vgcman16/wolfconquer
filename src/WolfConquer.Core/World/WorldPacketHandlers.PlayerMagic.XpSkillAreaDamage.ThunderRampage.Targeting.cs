using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyThunderRampageMonsterAndSobNpcRadius = 11;
    private const byte LegacyThunderRampagePlayerRadius = 9;

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyThunderRampageMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) <= LegacyThunderRampageMonsterAndSobNpcRadius)
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyThunderRampagePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(
                    query.X,
                    query.Y,
                    character.X,
                    character.Y) <= LegacyThunderRampagePlayerRadius)
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyThunderRampageSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) <= LegacyThunderRampageMonsterAndSobNpcRadius)
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }
}
