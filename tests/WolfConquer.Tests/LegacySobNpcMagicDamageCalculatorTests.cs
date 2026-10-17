using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySobNpcMagicDamageCalculatorTests
{
    [Fact]
    public void CalculateDamageAddsMagicAttackAndSpellDamage()
    {
        var damage = LegacySobNpcMagicDamageCalculator.CalculateDamage(
            magicAttack: 1_200,
            magicPercent: 0,
            magicDamageIncrease: 0,
            CreateSpell(damage: 300));

        Assert.Equal(1_500U, damage);
    }

    [Fact]
    public void CalculateDamageAppliesPercentBonusBeforeFlatIncrease()
    {
        var damage = LegacySobNpcMagicDamageCalculator.CalculateDamage(
            magicAttack: 1_000,
            magicPercent: 25,
            magicDamageIncrease: 50,
            CreateSpell(damage: 200));

        Assert.Equal(1_550U, damage);
    }

    [Fact]
    public void CalculateDamageReturnsMinimumOne()
    {
        var damage = LegacySobNpcMagicDamageCalculator.CalculateDamage(
            magicAttack: 0,
            magicPercent: 0,
            magicDamageIncrease: 0,
            CreateSpell(damage: 0));

        Assert.Equal(1U, damage);
    }

    private static LegacyMagicRuntimeSpell CreateSpell(uint damage)
    {
        return new LegacyMagicRuntimeSpell(
            Id: 1_000,
            Sort: LegacyMagicSort.Attack,
            Name: "Thunder",
            Level: 0,
            UseMana: 0,
            Damage: damage,
            Experience: 0,
            MaxTargets: 1,
            Range: 0,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);
    }
}
