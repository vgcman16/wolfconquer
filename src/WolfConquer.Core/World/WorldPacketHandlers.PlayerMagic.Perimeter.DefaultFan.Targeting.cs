using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacyPerimeterDefaultFanSpell(ushort spellId)
    {
        return spellId is 12_210 or 12_220 or 12_580 or 12_590 or 12_740 or 12_750;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyPerimeterDefaultFanMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (IsInsideLegacyPerimeterDefaultFan(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.Monster.X,
                    candidate.Monster.Y))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyPerimeterDefaultFanPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (IsInsideLegacyPerimeterDefaultFan(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    character.X,
                    character.Y))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyPerimeterDefaultFanSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query)
    {
        var targets = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (IsInsideLegacyPerimeterDefaultFan(
                    session.Character.X,
                    session.Character.Y,
                    query.X,
                    query.Y,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y))
            {
                targets.Add(candidate);
            }
        }

        return targets;
    }

    private static bool IsInsideLegacyPerimeterDefaultFan(
        long casterX,
        long casterY,
        long sourceX,
        long sourceY,
        long targetX,
        long targetY)
    {
        if (LegacyGetDistance(casterX, casterY, targetX, targetY) > LegacyPerimeterDefaultFanRange)
        {
            return false;
        }

        var halfWidthRadians = Math.PI * LegacyPerimeterDefaultFanWidth / 180.0 / 2.0;
        var targetRadian = GetLegacyPerimeterDefaultFanRadian(casterX, casterY, targetX, targetY);
        var sourceRadian = GetLegacyPerimeterDefaultFanRadian(sourceX, sourceY, casterX, casterY);
        var difference = Math.Abs(targetRadian - sourceRadian);
        if (difference <= halfWidthRadians || difference >= (2.0 * Math.PI) - halfWidthRadians)
        {
            return false;
        }

        return true;
    }

    private static float GetLegacyPerimeterDefaultFanRadian(
        long sourceX,
        long sourceY,
        long targetX,
        long targetY)
    {
        var deltaX = (float)(targetX - sourceX);
        var deltaY = (float)(targetY - sourceY);
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var arcsine = MathF.Asin(deltaX / distance);
        return deltaY > 0
            ? (MathF.PI / 2F) - arcsine
            : MathF.PI + arcsine + (MathF.PI / 2F);
    }
}
