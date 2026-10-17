using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyPirateBombMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY,
        ushort spellLevel)
    {
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        var remaining = GetLegacyPirateBombTargetBudget(spellLevel);
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (remaining <= 0)
            {
                break;
            }

            if (LegacyGetDistance(originX, originY, candidate.Monster.X, candidate.Monster.Y) > LegacyPirateBombRadius)
            {
                continue;
            }

            selected.Add(candidate);
            remaining--;
        }

        return selected;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyCannonBarrageMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY)
    {
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(originX, originY, candidate.Monster.X, candidate.Monster.Y) > LegacyPirateBombRadius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyCannonBarragePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var selected = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(originX, originY, character.X, character.Y) > LegacyPirateBombRadius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyCannonBarrageSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY)
    {
        var selected = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(originX, originY, candidate.SobNpc.X, candidate.SobNpc.Y) > LegacyPirateBombRadius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyPirateBombPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY,
        int targetBudget)
    {
        var selected = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            if (selected.Count >= targetBudget)
            {
                break;
            }

            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(originX, originY, character.X, character.Y) > LegacyPirateBombRadius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyPirateBombSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY,
        int targetBudget)
    {
        var selected = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (selected.Count >= targetBudget)
            {
                break;
            }

            if (LegacyGetDistance(originX, originY, candidate.SobNpc.X, candidate.SobNpc.Y) > LegacyPirateBombRadius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static int CountLegacyPirateBombPendingPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var count = 0;
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            if (LegacyGetDistance(
                    originX,
                    originY,
                    candidate.ActiveSession.Session.Character.X,
                    candidate.ActiveSession.Session.Character.Y) <= LegacyPirateBombRadius)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountLegacyPirateBombPendingSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort originX,
        ushort originY)
    {
        var count = 0;
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(originX, originY, candidate.SobNpc.X, candidate.SobNpc.Y) <= LegacyPirateBombRadius)
            {
                count++;
            }
        }

        return count;
    }

    private static string GetLegacyPirateBombSkillName(LegacyMagicSort sort)
    {
        return sort switch
        {
            LegacyMagicSort.ScurvyBomb => "ScurvyBomb",
            LegacyMagicSort.CannonBarrage => "CannonBarrage",
            LegacyMagicSort.PirateXpSkill => "PirateXpSkill",
            _ => "BombPirte/GaleBomb",
        };
    }

    private static int GetLegacyPirateBombTargetBudget(ushort spellLevel)
    {
        return spellLevel switch
        {
            0 or 1 => 3,
            2 or 3 => 4,
            _ => 5,
        };
    }
}
