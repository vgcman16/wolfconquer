using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyPlayerPlayerPhysicalSpellDamageResult FinishLegacyCombatGearTriple17490PlayerDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData,
        LegacyPlayerPlayerPhysicalSpellDamageResult damageResult,
        LegacyPlayerPlayerPhysicalBreakthroughState breakthroughState)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var damage = damageResult.Damage;
        var effect = damageResult.Effect;
        var tortoise = ResolveLegacyPlayerPlayerTargetTortoiseDamagePercent(target, targetFields);
        if (HasLegacyStatusFlag(attacker.Character, LegacyCombatGearDragonPierceTortoiseFlag))
        {
            tortoise = ApplyLegacyBonePulseTortoiseReduction(
                tortoise, ReadAttributeUInt32(
                    attacker.Character.Attributes, RuntimeLegacyCombatGearDragonPierceTortoiseAttribute));
        }

        if (HasLegacyStatusFlag(attacker.Character, LegacyBonePulseStatusFlag))
        {
            effect |= MsgAttackEffect.BonePulseDestroy;
            tortoise = ApplyLegacyBonePulseTortoiseReduction(tortoise, ReadLegacyBonePulsePower(attacker.Character));
        }

        damage -= MulDivToUInt32(damage, Math.Min(tortoise, 25U), 100);
        if (target.Character.Class is >= 10_000 and <= 14_051)
        {
            damage -= MulDivToUInt32(damage, Math.Min(tortoise, 20U), 100);
        }

        if (HasLegacyStatusFlag(target.Character, LegacyCombatGearImmersionFlag))
        {
            damage -= MulDivToUInt32(
                damage, ReadAttributeUInt32(
                    attacker.Character.Attributes, RuntimeLegacyCombatGearImmersionPowerAttribute), 100);
        }

        damage = BigMulDivToUInt32(damage, target.Character.Reborn >= 2 ? 5_000U : 10_000U, 10_000);
        damage -= MulDivToUInt32(damage, targetFields[LegacyPlayerStatusPacket.ItemBlessIndex], 100);
        damage = ApplyLegacyPlayerPlayerPhysicalExtraAttack(
            damage,
            attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        if (breakthroughState.Penetrated)
        {
            damage = ApplyLegacyPlayerPlayerPhysicalPenetrationCompensation(attacker, target, spell, damage);
        }

        damage = Math.Max(1, damage);
        if (attacker.Character.Class is >= 10_000 and <= 14_051)
        {
            damage = MulDivToUInt32(damage, 15, 25);
        }

        return damageResult with { Damage = damage, Effect = effect };
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult ApplyLegacyCombatGearTriple17490BattlePowerAdvantage(
        WorldSession attacker,
        WorldSession target,
        WorldDataCatalog? worldData,
        LegacyPlayerPlayerPhysicalSpellDamageResult damageResult)
    {
        if (worldData is null)
        {
            return damageResult;
        }

        var attackerPower = CalculateLegacyPlayerPlayerPhysicalBattlePower(attacker, worldData);
        var targetPower = CalculateLegacyPlayerPlayerPhysicalBattlePower(target, worldData);
        if (attackerPower <= targetPower)
        {
            return damageResult;
        }

        var difference = checked((uint)Math.Min(uint.MaxValue, attackerPower - targetPower));
        return damageResult with
        {
            Damage = AddLegacySaturating(
                damageResult.Damage, MulDivToUInt32(damageResult.Damage, difference, 70)),
        };
    }
}
