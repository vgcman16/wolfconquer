using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TrySelectLegacyPlayerMonsterMagicTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        out IReadOnlyList<LegacyPlayerMonsterMagicTarget> targets,
        out string failureDiagnostic)
    {
        targets = [];
        failureDiagnostic = string.Empty;

        if (!TryResolveLegacyMagicOrigin(context, session, query, out var originX, out var originY))
        {
            failureDiagnostic = $"monsterUid={query.OpponentUid}; target is not visible";
            return false;
        }

        List<LegacyPlayerMonsterMagicTarget> selected;
        switch (spell.Sort)
        {
            case LegacyMagicSort.Attack:
                if (!TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var target))
                {
                    failureDiagnostic = $"monsterUid={query.OpponentUid}; target is not visible";
                    return false;
                }

                if (!CanLegacyPlayerAttackMonster(
                        session.Character,
                        target.Monster,
                        target.MonsterFamily,
                        out failureDiagnostic))
                {
                    failureDiagnostic = $"monsterUid={query.OpponentUid}; {failureDiagnostic}";
                    return false;
                }

                var directDistance = LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    target.Monster.X,
                    target.Monster.Y);
                if (directDistance > spell.Range)
                {
                    failureDiagnostic =
                        $"monsterUid={target.Monster.Uid} distance={directDistance} range={spell.Range}";
                    return false;
                }

                selected = [target];
                break;
            case LegacyMagicSort.DirectAttack:
                selected = SelectLegacyDirectLineMonsterTargets(context, session, query, spell, originX, originY);
                break;
            case LegacyMagicSort.Line:
            case LegacyMagicSort.ManualLine:
                selected = SelectLegacyLineMonsterTargets(context, session, spell, originX, originY);
                break;
            case LegacyMagicSort.Sector:
                selected = SelectLegacySectorMonsterTargets(context, session, spell, originX, originY);
                break;
            case LegacyMagicSort.Perimeter:
                selected = SelectLegacyPerimeterMonsterTargets(context, session, spell, originX, originY);
                break;
            case LegacyMagicSort.Bomb:
                selected = SelectLegacyBombMonsterTargets(context, session, spell, originX, originY);
                break;
            default:
                failureDiagnostic = $"sort={(int)spell.Sort}; unsupported monster magic sort";
                return false;
        }

        if (selected.Count == 0)
        {
            failureDiagnostic =
                $"monsterUid={query.OpponentUid} x={originX} y={originY}; no visible monsters matched shape";
            return false;
        }

        targets = selected;
        return true;
    }

    private static bool TrySelectLegacyPlayerMagicTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession primaryTarget,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        out IReadOnlyList<LegacyPlayerMagicTarget> targets,
        out string failureDiagnostic,
        out LegacyPlayerMagicGateFailureEffect failureEffect)
    {
        targets = [];
        failureDiagnostic = string.Empty;
        failureEffect = LegacyPlayerMagicGateFailureEffect.None;

        ushort originX = query.X;
        ushort originY = query.Y;
        if (spell.Sort != LegacyMagicSort.Attack &&
            !TryResolveLegacyMagicOrigin(context, session, query, out originX, out originY))
        {
            failureDiagnostic = $"playerUid={query.OpponentUid}; target is not visible";
            return false;
        }

        List<LegacyPlayerMagicTarget> selected;
        switch (spell.Sort)
        {
            case LegacyMagicSort.Attack:
                if (!CanLegacyPlayerMagicDamageTarget(
                        context,
                        session,
                        primaryTarget.Session,
                        spell,
                        out failureDiagnostic,
                        out failureEffect))
                {
                    failureDiagnostic = $"playerUid={query.OpponentUid}; {failureDiagnostic}";
                    return false;
                }

                var directDistance = LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    primaryTarget.Session.Character.X,
                    primaryTarget.Session.Character.Y);
                if (directDistance > spell.Range)
                {
                    failureDiagnostic =
                        $"playerUid={query.OpponentUid} distance={directDistance} range={spell.Range}";
                    return false;
                }

                selected = [new LegacyPlayerMagicTarget(primaryTarget)];
                break;
            case LegacyMagicSort.DirectAttack:
                selected = SelectLegacyDirectLinePlayerTargets(
                    context,
                    session,
                    primaryTarget,
                    query,
                    spell,
                    originX,
                    originY);
                break;
            case LegacyMagicSort.Line:
            case LegacyMagicSort.ManualLine:
                selected = SelectLegacyLinePlayerTargets(context, session, spell, originX, originY);
                break;
            case LegacyMagicSort.Sector:
                selected = SelectLegacySectorPlayerTargets(context, session, spell, originX, originY);
                break;
            case LegacyMagicSort.Bomb:
                selected = SelectLegacyBombPlayerTargets(context, session, spell, originX, originY);
                break;
            default:
                failureDiagnostic = $"sort={(int)spell.Sort}; unsupported player magic sort";
                return false;
        }

        if (selected.Count == 0)
        {
            failureDiagnostic =
                $"playerUid={query.OpponentUid} x={originX} y={originY}; no visible players matched shape";
            return false;
        }

        var pkMode = ReadAttributeUInt64(session.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        var captureAcceptedTarget = pkMode == LegacyPkModeCapture &&
            selected.Any(target =>
                HasLegacyStatusFlag(target.ActiveSession.Session.Character, LegacyFlashingNameStatusFlag) ||
                HasLegacyStatusFlag(target.ActiveSession.Session.Character, LegacyBlackNameStatusFlag));
        if (!captureAcceptedTarget &&
            IsLegacyPlayerMagicBlockAttackMapBlocked(session.Character, out failureDiagnostic))
        {
            failureDiagnostic = $"playerUid={query.OpponentUid}; {failureDiagnostic}";
            return false;
        }

        targets = selected;
        return true;
    }

    private static bool TrySelectLegacySobNpcMagicTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc primaryTarget,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        out IReadOnlyList<LegacyPlayerSobNpcMagicTarget> targets,
        out string failureDiagnostic)
    {
        targets = [];
        failureDiagnostic = string.Empty;

        if (!TryResolveLegacyMagicOrigin(context, session, query, out var originX, out var originY))
        {
            failureDiagnostic = $"sobNpcUid={query.OpponentUid}; target is not visible";
            return false;
        }

        List<LegacyPlayerSobNpcMagicTarget> selected;
        switch (spell.Sort)
        {
            case LegacyMagicSort.Attack:
                if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, primaryTarget, out failureDiagnostic))
                {
                    failureDiagnostic = $"sobNpcUid={query.OpponentUid}; {failureDiagnostic}";
                    return false;
                }

                var directDistance = LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    primaryTarget.X,
                    primaryTarget.Y);
                if (directDistance > spell.Range)
                {
                    failureDiagnostic =
                        $"sobNpcUid={query.OpponentUid} distance={directDistance} range={spell.Range}";
                    return false;
                }

                selected = [new LegacyPlayerSobNpcMagicTarget(primaryTarget)];
                break;
            case LegacyMagicSort.DirectAttack:
                selected = SelectLegacyDirectLineSobNpcTargets(
                    context,
                    session,
                    primaryTarget,
                    query,
                    spell,
                    originX,
                    originY);
                break;
            case LegacyMagicSort.Line:
            case LegacyMagicSort.ManualLine:
                selected = SelectLegacyLineSobNpcTargets(context, session, spell, originX, originY);
                break;
            case LegacyMagicSort.Sector:
                selected = SelectLegacySectorSobNpcTargets(context, session, spell, originX, originY);
                break;
            case LegacyMagicSort.Bomb:
                selected = SelectLegacyBombSobNpcTargets(context, session, spell, originX, originY);
                break;
            default:
                failureDiagnostic = $"sort={(int)spell.Sort}; unsupported SOB magic sort";
                return false;
        }

        if (selected.Count == 0)
        {
            failureDiagnostic =
                $"sobNpcUid={query.OpponentUid} x={originX} y={originY}; no visible SOB NPCs matched shape";
            return false;
        }

        targets = selected;
        return true;
    }

    private static bool TryResolveLegacyMagicOrigin(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        out ushort x,
        out ushort y)
    {
        if (query.X != 0 || query.Y != 0)
        {
            x = query.X;
            y = query.Y;
            return true;
        }

        if (TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var target))
        {
            x = target.Monster.X;
            y = target.Monster.Y;
            return true;
        }

        if (TryFindVisibleSobNpcByUid(context, session.Character, query.OpponentUid, out var sobNpc))
        {
            x = CheckedUInt16(sobNpc.X, nameof(sobNpc.X));
            y = CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y));
            return true;
        }

        if (TryGetVisibleSessionByUid(context, session, query.OpponentUid, out var activeTarget))
        {
            x = CheckedUInt16(activeTarget.Session.Character.X, nameof(activeTarget.Session.Character.X));
            y = CheckedUInt16(activeTarget.Session.Character.Y, nameof(activeTarget.Session.Character.Y));
            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyDirectLineMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        if (!TryFindVisibleMonsterTargetByUid(context, session.Character, query.OpponentUid, out var primary))
        {
            return [];
        }

        if (!CanLegacyPlayerAttackMonster(session.Character, primary.Monster, primary.MonsterFamily, out _))
        {
            return [];
        }

        if (LegacyGetDistance(session.Character.X, session.Character.Y, primary.Monster.X, primary.Monster.Y) > spell.Range)
        {
            return [];
        }

        var selected = new List<LegacyPlayerMonsterMagicTarget> { primary };
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (candidate.Monster.Uid == primary.Monster.Uid ||
                LegacyGetDistance(session.Character.X, session.Character.Y, candidate.Monster.X, candidate.Monster.Y) > spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    spell.Range,
                    width: 0))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyLineMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var width = LegacyMagicRuntime.GetLegacyLineWidth(spell);
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(session.Character.X, session.Character.Y, candidate.Monster.X, candidate.Monster.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    spell.Range,
                    width))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacySectorMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        var sectorSize = LegacyMagicRuntime.GetLegacySectorSize(spell);
        var distance = LegacyMagicRuntime.GetLegacySectorDistance(spell);
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (!IsInsideLegacySector(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    sectorSize,
                    distance))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyPerimeterMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var (sectorSize, distance) = spell.Id switch
        {
            13_040 => (140, (byte)7),
            14_570 or 14_580 => (60, (byte)3),
            15_670 or 15_680 => (500, (byte)5),
            _ => (0, (byte)0),
        };
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (!IsInsideLegacySector(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.Monster.X,
                    candidate.Monster.Y,
                    sectorSize,
                    distance) ||
                !CanLegacyPlayerAttackMonster(
                    session.Character,
                    candidate.Monster,
                    candidate.MonsterFamily,
                    out _))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyBombMonsterTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var radius = LegacyMagicRuntime.GetLegacyBombRadius(spell);
        var selected = new List<LegacyPlayerMonsterMagicTarget>();
        foreach (var candidate in EnumerateVisibleMonsterTargets(context, session.Character))
        {
            if (LegacyGetDistance(originX, originY, candidate.Monster.X, candidate.Monster.Y) > radius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyDirectLineSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        SobNpc primaryTarget,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        if (!CanLegacySobNpcMagicDamageTarget(context, session.Character, primaryTarget, out _) ||
            LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                primaryTarget.X,
                primaryTarget.Y) > spell.Range)
        {
            return [];
        }

        var selected = new List<LegacyPlayerSobNpcMagicTarget> { new(primaryTarget) };
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if ((candidate.SobNpc.Id == primaryTarget.Id &&
                    candidate.SobNpc.MapId == primaryTarget.MapId &&
                    candidate.SobNpc.SourceLine == primaryTarget.SourceLine) ||
                LegacyGetDistance(session.Character.X, session.Character.Y, candidate.SobNpc.X, candidate.SobNpc.Y) > spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    spell.Range,
                    width: 0))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyLineSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var width = LegacyMagicRuntime.GetLegacyLineWidth(spell);
        var selected = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(session.Character.X, session.Character.Y, candidate.SobNpc.X, candidate.SobNpc.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    spell.Range,
                    width))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacySectorSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var selected = new List<LegacyPlayerSobNpcMagicTarget>();
        var sectorSize = LegacyMagicRuntime.GetLegacySectorSize(spell);
        var distance = LegacyMagicRuntime.GetLegacySectorDistance(spell);
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (!IsInsideLegacySector(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    candidate.SobNpc.X,
                    candidate.SobNpc.Y,
                    sectorSize,
                    distance))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyBombSobNpcTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var radius = LegacyMagicRuntime.GetLegacyBombRadius(spell);
        var selected = new List<LegacyPlayerSobNpcMagicTarget>();
        foreach (var candidate in EnumerateVisibleSobNpcMagicTargets(context, session.Character))
        {
            if (LegacyGetDistance(originX, originY, candidate.SobNpc.X, candidate.SobNpc.Y) > radius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyDirectLinePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        WorldActiveSession primaryTarget,
        MsgAttackQuery query,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        if (!CanLegacyPlayerMagicDamageTarget(context, session, primaryTarget.Session, spell, out _) ||
            LegacyGetDistance(
                session.Character.X,
                session.Character.Y,
                primaryTarget.Session.Character.X,
                primaryTarget.Session.Character.Y) > spell.Range)
        {
            return [];
        }

        var selected = new List<LegacyPlayerMagicTarget> { new(primaryTarget) };
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (character.Uid == primaryTarget.Session.Character.Uid ||
                LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) > spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    character.X,
                    character.Y,
                    spell.Range,
                    width: 0))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyLinePlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var width = LegacyMagicRuntime.GetLegacyLineWidth(spell);
        var selected = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(session.Character.X, session.Character.Y, character.X, character.Y) >= spell.Range ||
                !IsLegacyInLine(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    character.X,
                    character.Y,
                    spell.Range,
                    width))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacySectorPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var selected = new List<LegacyPlayerMagicTarget>();
        var sectorSize = LegacyMagicRuntime.GetLegacySectorSize(spell);
        var distance = LegacyMagicRuntime.GetLegacySectorDistance(spell);
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (!IsInsideLegacySector(
                    session.Character.X,
                    session.Character.Y,
                    originX,
                    originY,
                    character.X,
                    character.Y,
                    sectorSize,
                    distance))
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyBombPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        ushort originX,
        ushort originY)
    {
        var radius = LegacyMagicRuntime.GetLegacyBombRadius(spell);
        var selected = new List<LegacyPlayerMagicTarget>();
        foreach (var candidate in EnumerateVisiblePlayerMagicTargets(context, session, spell))
        {
            var character = candidate.ActiveSession.Session.Character;
            if (LegacyGetDistance(originX, originY, character.X, character.Y) > radius)
            {
                continue;
            }

            selected.Add(candidate);
        }

        return selected;
    }
}
