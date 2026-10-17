using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyPlayerPlayerPhysicalSpellDamageResult CalculateLegacyCombatGearTriple17490PlayerDamage(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        WorldDataCatalog? worldData)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var damage = GetLegacyDeterministicAttack(
            attackerFields[LegacyPlayerStatusPacket.MaxAttackIndex],
            attackerFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        damage = ApplyLegacyPlayerAjustAttack(damage, attackerFields, attacker.Character);
        var attributeDefense = ReadAttributeUInt32(
            target.Character.Attributes, "AjustDefense", "AdjustDefense", "Defense", "Defence");
        var defense = Math.Max(attributeDefense, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        damage = damage <= defense ? 1 : damage - defense;
        var percent = ResolveLegacyCombatGearTriple17490Bonus(spell, player: true);
        damage = AddLegacySaturating(damage, MulDivToUInt32(damage, percent, 100));
        return new LegacyPlayerPlayerPhysicalSpellDamageResult(damage, MsgAttackEffect.None);
    }

    private static LegacyMagicRuntimeSpell GetLegacyCombatGearTriple17490NonPlayerScalar(
        LegacyMagicRuntimeSpell spell)
    {
        return spell with { Damage = 100 + ResolveLegacyCombatGearTriple17490Bonus(spell, player: false) };
    }

    private static uint ResolveLegacyCombatGearTriple17490Bonus(LegacyMagicRuntimeSpell spell, bool player)
    {
        var rawGDamage = spell.RawDamage != 0 ? spell.RawDamage : spell.Damage;
        var selected = ResolveLegacyCombatGearDamagePercentCandidate(rawGDamage, rawGDamage, isGDamage: true);
        Include(ResolveLegacyCombatGearDamagePercentCandidate(spell.DamageOnHuman, rawGDamage));
        Include(ResolveLegacyCombatGearDamagePercentCandidate(spell.Damage2, rawGDamage));
        Include(ResolveLegacyCombatGearDamagePercentCandidate(spell.Damage3, rawGDamage));
        Include(ResolveLegacyCombatGearDamagePercentCandidate(spell.DamageOnMonster, rawGDamage));
        return selected;

        void Include(uint candidate)
        {
            selected = player ? Math.Min(selected, candidate) : Math.Max(selected, candidate);
        }
    }
}
