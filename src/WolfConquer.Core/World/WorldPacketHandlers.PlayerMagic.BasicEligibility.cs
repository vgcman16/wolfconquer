namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Deterministic basic eligibility only. Do not add counter/rune/rate rolls,
    // publication, status insertion or resource mutation to this shared query.
    private static bool CanLegacyPlayerMagicBasicEligibility(
        WorldSession caster, WorldSession target, LegacyMagicRuntimeSpell spell,
        out ulong pkMode, out string failureDiagnostic, out bool safeZoneDenied)
    {
        pkMode = 0;
        failureDiagnostic = string.Empty;
        safeZoneDenied = false;
        if (TryGetLegacyPlayerMagicSafeZone(
                caster.Character.MapId,
                target.Character.X,
                target.Character.Y,
                out var safeZone))
        {
            safeZoneDenied = true;
            failureDiagnostic =
                $"map={caster.Character.MapId} targetX={target.Character.X} targetY={target.Character.Y}; " +
                $"source CanAttackPlayer.Verified rejected {safeZone} safe zone: You can't PK in the safe zone";
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
            failureDiagnostic =
                "target Invisible attribute is set; source CanAttackPlayer.Verified rejected attacked.Invisible";
            return false;
        }

        if (HasLegacyStatusFlag(target.Character, LegacyNeptuneCurseFlag))
        {
            failureDiagnostic = "target has NeptuneCurse";
            return false;
        }

        if (HasLegacyStatusFlag(target.Character, LegacyFlyFlag) &&
            !spell.AttackInFly)
        {
            failureDiagnostic =
                "target is flying and spell AttackInFly is false; source CanAttackPlayer.Verified rejected !DBSpell.AttackInFly";
            return false;
        }

        if (HasLegacyPlayerMagicWatchingAttribute(target.Character))
        {
            failureDiagnostic =
                "target watching attribute is set; source CanAttackPlayer.Verified rejected attacked.Owner.IsWatching()";
            return false;
        }

        if (HasLegacyPlayerMagicWatchingAttribute(caster.Character))
        {
            failureDiagnostic =
                "caster watching attribute is set; source CanAttackPlayer.Verified rejected client.IsWatching()";
            return false;
        }

        if (IsLegacyPlayerAttackProtected(target.Character, DateTimeOffset.UtcNow))
        {
            failureDiagnostic = "target legacy Protect window is active";
            return false;
        }

        if (target.Character.MapId == 700 &&
            ReadCharacterDynamicId(target.Character) == 0)
        {
            failureDiagnostic = "target map=700 dynamicId=0 blocks player-target magic";
            return false;
        }

        pkMode = ReadAttributeUInt64(caster.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        if (pkMode == LegacyPkModePeace)
        {
            failureDiagnostic = "caster is in Peace PK mode";
            return false;
        }

        return true;
    }
}
