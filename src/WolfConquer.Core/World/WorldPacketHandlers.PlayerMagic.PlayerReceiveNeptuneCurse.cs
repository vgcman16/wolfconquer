using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyPlayerReceiveNeptuneCurseResult(
        uint Damage,
        CharacterSnapshot Attacker,
        bool AttackHitApplied,
        bool TargetNeptuneCurseBlocked,
        bool AttackerNeptuneCurseCapped);

    private static LegacyPlayerReceiveNeptuneCurseResult ApplyLegacyPlayerReceiveNeptuneCurse(
        CharacterSnapshot attacker,
        CharacterSnapshot target,
        uint damage)
    {
        var updatedAttacker = WithCharacterAttribute(attacker, RuntimeLegacyAttackHitAttribute, "true");
        var adjustedDamage = damage;
        var targetNeptuneCurseBlocked = false;
        var attackerNeptuneCurseCapped = false;

        if (HasLegacyStatusFlag(target, LegacyNeptuneCurseFlag))
        {
            adjustedDamage = 0;
            targetNeptuneCurseBlocked = true;
        }
        else if (HasLegacyStatusFlag(attacker, LegacyNeptuneCurseFlag))
        {
            var targetHitpoints = CheckedUInt32(target.MinHitPoints, nameof(target.MinHitPoints));
            if (adjustedDamage < targetHitpoints)
            {
                adjustedDamage = targetHitpoints - 1;
                attackerNeptuneCurseCapped = true;
            }
        }

        return new LegacyPlayerReceiveNeptuneCurseResult(
            adjustedDamage,
            updatedAttacker,
            AttackHitApplied: true,
            targetNeptuneCurseBlocked,
            attackerNeptuneCurseCapped);
    }
}
