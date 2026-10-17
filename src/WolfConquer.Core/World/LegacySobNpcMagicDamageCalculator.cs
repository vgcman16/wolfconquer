namespace WolfConquer.Core.World;

public static class LegacySobNpcMagicDamageCalculator
{
    public static uint CalculateDamage(
        uint magicAttack,
        uint magicPercent,
        uint magicDamageIncrease,
        LegacyMagicRuntimeSpell spell)
    {
        var damage = LegacyCombatDamageCalculator.AddLegacySaturating(magicAttack, spell.Damage);
        damage = LegacyCombatDamageCalculator.AddLegacyPercentBonus(damage, magicPercent);
        damage = LegacyCombatDamageCalculator.AddLegacySaturating(damage, magicDamageIncrease);
        return Math.Max(1, damage);
    }
}
