using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static List<LegacyPlayerMonsterMagicTarget> SelectLegacyWhirlwindKickMonsterTargets(
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
                    candidate.Monster.Y) >= LegacyWhirlwindKickRadius)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static List<LegacyPlayerMagicTarget> SelectLegacyWhirlwindKickPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        var targets = new List<LegacyPlayerMagicTarget>();
        var candidates = spell.Id == LegacyWhirlwindKickSpellId
            ? EnumerateVisibleLegacyWhirlwindKickPlayerTargets(context, session, spell)
            : EnumerateVisiblePlayerMagicTargets(context, session, spell);
        foreach (var candidate in candidates)
        {
            var targetSession = candidate.ActiveSession.Session;
            var character = targetSession.Character;
            if (LegacyGetDistance(
                    session.Character.X,
                    session.Character.Y,
                    character.X,
                    character.Y) >= LegacyWhirlwindKickRadius)
            {
                continue;
            }

            if (spell.Id != LegacyWhirlwindKickSpellId &&
                !CanLegacyPlayerMagicDamageTarget(context, session, targetSession, spell, out _))
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static IEnumerable<LegacyPlayerMagicTarget> EnumerateVisibleLegacyWhirlwindKickPlayerTargets(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMagicRuntimeSpell spell)
    {
        if (context.Sessions is not { } sessions)
        {
            yield break;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeePlayer(session.Character, active.Session.Character) ||
                !CanLegacyWhirlwindKickDamageTarget(context, session, active.Session, spell, out _))
            {
                continue;
            }

            yield return new LegacyPlayerMagicTarget(active);
        }
    }

    private static bool CanLegacyWhirlwindKickDamageTarget(
        WorldPacketHandlerContext context,
        WorldSession caster,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        out string failureDiagnostic)
    {
        failureDiagnostic = string.Empty;
        if (TryGetLegacyCombatGearDamageBlockReason(target.Character, out var combatGearReason))
        {
            failureDiagnostic =
                $"target legacy CombatGear CheckDamage blocked WhirlwindKick reason={combatGearReason}";
            return false;
        }

        if (TryGetLegacyPlayerMagicSafeZone(
                caster.Character.MapId,
                target.Character.X,
                target.Character.Y,
                out var safeZone))
        {
            failureDiagnostic = $"source CanAttackWhirlwindKick.Verified rejected {safeZone} safe zone";
            return false;
        }

        if (target.Character.MapId == 700 && ReadCharacterDynamicId(target.Character) == 0)
        {
            failureDiagnostic = "target map=700 dynamicId=0 blocks WhirlwindKick";
            return false;
        }

        if (target.Character.Name.Contains("[GM]", StringComparison.Ordinal) ||
            target.Character.Name.Contains("[PM]", StringComparison.Ordinal))
        {
            failureDiagnostic = "source CanAttackWhirlwindKick.Verified rejected GM/PM target name";
            return false;
        }

        if (HasLegacyStatusFlag(target.Character, LegacyNeptuneCurseFlag))
        {
            failureDiagnostic = "target has NeptuneCurse";
            return false;
        }

        if (target.Character.MinHitPoints <= 0 ||
            HasLegacyStatusFlag(target.Character, LegacyDeadFlag) ||
            HasLegacyStatusFlag(target.Character, LegacyGhostFlag))
        {
            failureDiagnostic = "target is dead or ghosted";
            return false;
        }

        if (ReadAttributeBool(target.Character.Attributes, "Invisible"))
        {
            failureDiagnostic = "target Invisible attribute is set";
            return false;
        }

        var pkMode = ReadAttributeUInt64(caster.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        if (pkMode == LegacyPkModePeace)
        {
            failureDiagnostic = "caster is in Peace PK mode";
            return false;
        }

        if (IsLegacyPlayerMagicTournamentStateBlocked(caster.Character, out failureDiagnostic) ||
            HasLegacyPlayerMagicWatchingAttribute(target.Character) ||
            HasLegacyPlayerMagicWatchingAttribute(caster.Character) ||
            IsLegacyPlayerAttackProtected(target.Character, DateTimeOffset.UtcNow) ||
            IsLegacyPlayerMagicCaptureTheFlagAttackBlocked(caster.Character, target.Character, out failureDiagnostic) ||
            IsLegacyPlayerMagicSameGarmentMapBlocked(caster, target, out failureDiagnostic))
        {
            return false;
        }

        if ((pkMode == LegacyPkModeTeam || pkMode == LegacyPkModeCs || pkMode == LegacyPkModeUnion) &&
            IsLegacyPlayerMagicFriendlyFireBlocked(context, caster, target, pkMode, out failureDiagnostic))
        {
            return false;
        }

        if (IsLegacyPlayerMagicBlockedMap(caster.Character.MapId, pkMode))
        {
            failureDiagnostic = $"map={caster.Character.MapId} blocks WhirlwindKick player damage";
            return false;
        }

        if (IsLegacyPlayerMagicTeamQualifierSameTeamBlocked(context, caster, target, out failureDiagnostic) ||
            IsLegacyPlayerMagicOwnServerBlocked(caster.Character, out failureDiagnostic))
        {
            return false;
        }

        if (pkMode == LegacyPkModeCapture)
        {
            if (HasLegacyStatusFlag(target.Character, LegacyFlashingNameStatusFlag) ||
                HasLegacyStatusFlag(target.Character, LegacyBlackNameStatusFlag))
            {
                return true;
            }

            failureDiagnostic = "caster is in Capture PK mode and target is not FlashingName or BlackName";
            return false;
        }

        if (pkMode == LegacyPkModeJiang &&
            TryApplyLegacyPlayerMagicJiangPkGate(
                caster.Character,
                target.Character,
                out var canJiangAttack,
                out failureDiagnostic))
        {
            return canJiangAttack;
        }

        if (IsLegacyPlayerMagicBlockAttackMapBlocked(caster.Character, out failureDiagnostic))
        {
            return false;
        }

        _ = spell;
        return true;
    }

    private static List<LegacyPlayerSobNpcMagicTarget> SelectLegacyWhirlwindKickSobNpcTargets(
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
                    candidate.SobNpc.Y) >= LegacyWhirlwindKickRadius)
            {
                continue;
            }

            targets.Add(candidate);
        }

        return targets;
    }

    private static LegacyWhirlwindKickPlayerRuneAvoidance GetLegacyWhirlwindKickPlayerRuneAvoidance(
        WorldSession targetSession,
        WorldDataCatalog? worldData)
    {
        if (TryGetLegacyEquippedRuneLevel(
                targetSession.PlayerItems,
                worldData,
                "NatureShield",
                out var natureShieldLevel) &&
            CheckLegacyBombPirteRateRoll(
                targetSession.Character,
                GetLegacyBombPirteNatureShieldAvoidanceRate(natureShieldLevel),
                RuntimeLegacyWhirlwindKickNatureShieldRollAttribute))
        {
            return LegacyWhirlwindKickPlayerRuneAvoidance.NatureShield;
        }

        if (HasLegacyEquippedRune(targetSession.PlayerItems, worldData, "Acalanatha") &&
            CheckLegacyBombPirteRateRoll(
                targetSession.Character,
                55,
                RuntimeLegacyWhirlwindKickAcalanathaRollAttribute))
        {
            return LegacyWhirlwindKickPlayerRuneAvoidance.Acalanatha;
        }

        return LegacyWhirlwindKickPlayerRuneAvoidance.None;
    }
}

