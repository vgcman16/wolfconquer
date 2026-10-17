namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyPounceMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y) > LegacyPounceRadius)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyPouncePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        if (context.Sessions is not { } sessions)
        {
            return targets;
        }

        foreach (var active in sessions.Snapshot())
        {
            var character = active.Session.Character;
            if (ReferenceEquals(active.Connection, context.Connection) ||
                character.Uid == session.Character.Uid ||
                !CanSeePlayer(session.Character, character) ||
                LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y) > LegacyPounceRadius)
            {
                continue;
            }

            targets.Add(new LegacyPlayerMagicTarget(active));
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyPounceSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y) > LegacyPounceRadius)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }
}
