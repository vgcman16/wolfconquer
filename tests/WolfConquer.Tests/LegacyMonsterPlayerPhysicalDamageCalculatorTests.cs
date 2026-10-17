using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterPlayerPhysicalDamageCalculatorTests
{
    [Fact]
    public void SelectedAttackDamageFloorsToOneWhenAdjustedDefenseCoversDamage()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttack(
            selectedAttack: 731,
            playerAdjustedDefense: 29_051,
            playerBlessPercent: 0);

        Assert.Equal(1U, damage);
    }

    [Fact]
    public void StatusFieldsApplyGearDefenseBlessAndTortoiseWithLegacyOrder()
    {
        var ungearedFields = new uint[LegacyPlayerStatusPacket.FieldCount];
        var gearedFields = new uint[LegacyPlayerStatusPacket.FieldCount];
        gearedFields[LegacyPlayerStatusPacket.DefenceIndex] = 500;
        gearedFields[LegacyPlayerStatusPacket.ItemBlessIndex] = 20;
        gearedFields[LegacyPlayerStatusPacket.DamageIndex] = 10;

        var ungearedDamage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromAttackRangeWithStatusFields(
            monsterMinAttack: 2_000,
            monsterMaxAttack: 2_001,
            attackRoll: 0,
            targetStatusFields: ungearedFields);
        var gearedDamage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromAttackRangeWithStatusFields(
            monsterMinAttack: 2_000,
            monsterMaxAttack: 2_001,
            attackRoll: 0,
            targetStatusFields: gearedFields,
            physicalDamagePercent: 10);

        Assert.Equal(2_000U, ungearedDamage);
        Assert.Equal(1_210U, gearedDamage);
    }

    [Fact]
    public void StatusFieldsCanApplySourceAjustDefenseStateStacking()
    {
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.DefenceIndex] = 999;
        fields[LegacyPlayerStatusPacket.DamageIndex] = 10;
        fields[LegacyPlayerStatusPacket.ItemBlessIndex] = 7;
        var adjustments = new LegacyPhysicalDefenseAdjustments(
            MagicShieldOrDefensePotion: true,
            DragonSwing: true,
            DivineGuard: true);

        var inputs = LegacyCombatGearDamageInputs.FromStatusFields(
            fields,
            physicalDefenseAdjustments: adjustments);
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithStatusFields(
            selectedAttack: 4_000,
            targetStatusFields: fields,
            playerDefenseAdjustments: adjustments);

        Assert.Equal(3_444U, inputs.AdjustedPhysicalDefense);
        Assert.Equal(276U, damage);
    }

    [Fact]
    public void SelectedAttackDamageAppliesBlessBeforeAdjustedDefense()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttack(
            selectedAttack: 1_000,
            playerAdjustedDefense: 300,
            playerBlessPercent: 20);

        Assert.Equal(500U, damage);
    }

    [Fact]
    public void RawDefenseDamageAppliesTortoiseGemPercentBeforeDefenseClamp()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithDefense(
            selectedAttack: 1_000,
            playerPhysicalDefense: 500,
            playerBlessPercent: 20,
            playerTortoiseGemDamagePercent: 10);

        Assert.Equal(250U, damage);
    }

    [Fact]
    public void DodgeRollCanZeroPhysicalHit()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttack(
            selectedAttack: 1_000,
            playerAdjustedDefense: 100,
            playerBlessPercent: 0,
            playerDodge: 30,
            firstDodgeRoll: 29);

        Assert.Equal(0U, damage);
    }

    [Fact]
    public void DodgeRollUsesSecondLegacyRollWhenFirstRollIsAboveSixty()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttack(
            selectedAttack: 1_000,
            playerAdjustedDefense: 100,
            playerBlessPercent: 0,
            playerDodge: 30,
            firstDodgeRoll: 61,
            secondDodgeRoll: 30);

        Assert.Equal(900U, damage);
    }

    [Fact]
    public void DodgeRollMissesWhenSecondLegacyRollIsBelowDodge()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttack(
            selectedAttack: 1_000,
            playerAdjustedDefense: 100,
            playerBlessPercent: 0,
            playerDodge: 30,
            firstDodgeRoll: 61,
            secondDodgeRoll: 29);

        Assert.Equal(0U, damage);
    }

    [Fact]
    public void AttackRangeUsesDeterministicLegacyRoll()
    {
        var selectedAttack = LegacyMonsterPlayerPhysicalDamageCalculator.SelectLegacyAttack(
            monsterMinAttack: 100,
            monsterMaxAttack: 110,
            attackRoll: 14);
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromAttackRange(
            monsterMinAttack: 100,
            monsterMaxAttack: 110,
            attackRoll: 14,
            playerAdjustedDefense: 25,
            playerBlessPercent: 0);

        Assert.Equal(104U, selectedAttack);
        Assert.Equal(79U, damage);
    }

    [Fact]
    public void BossMinimumDamageUsesSelectedLegacyBossFloor()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttack(
            selectedAttack: 1_000,
            playerAdjustedDefense: 950,
            playerBlessPercent: 0,
            selectedBossMinimumDamage: 4_200);

        Assert.Equal(4_200U, damage);
    }

    [Fact]
    public void BossMinimumDamageDoesNotReplaceDamageAlreadyAtLegacyFloor()
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttack(
            selectedAttack: 3_000,
            playerAdjustedDefense: 0,
            playerBlessPercent: 0,
            selectedBossMinimumDamage: 4_200);

        Assert.Equal(3_000U, damage);
    }

    [Theory]
    [InlineData(0, 3_000)]
    [InlineData(3_499, 6_499)]
    [InlineData(3_500, 3_000)]
    public void BossMinimumDamageRollUsesLegacyExclusiveRange(uint roll, uint expectedDamage)
    {
        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.SelectBossMinimumDamage(roll);

        Assert.Equal(expectedDamage, damage);
    }
}
