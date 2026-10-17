using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCombatDamageCalculatorTests
{
    [Fact]
    public void MonsterPhysicalDamageFloorsToOneWhenGearDefenseCoversDamage()
    {
        var damage = LegacyCombatDamageCalculator.CalculateMonsterPhysicalDamage(
            monsterMinAttack: 731,
            monsterMaxAttack: 900,
            targetItemBless: 0,
            targetPhysicalDefense: 29_051);

        Assert.Equal(1U, damage);
    }

    [Fact]
    public void MonsterPhysicalDamageAppliesBlessBeforePhysicalDefense()
    {
        var damage = LegacyCombatDamageCalculator.CalculateMonsterPhysicalDamage(
            monsterMinAttack: 1_000,
            monsterMaxAttack: 2_000,
            targetItemBless: 20,
            targetPhysicalDefense: 300);

        Assert.Equal(500U, damage);
    }

    [Fact]
    public void MonsterPhysicalDamageAppliesTortoiseGemDefensePercentAfterBless()
    {
        var damage = LegacyCombatDamageCalculator.CalculateMonsterPhysicalDamage(
            monsterMinAttack: 1_000,
            monsterMaxAttack: 2_000,
            targetItemBless: 20,
            targetPhysicalDefense: 500,
            targetTortoiseGemDamagePercent: 10);

        Assert.Equal(250U, damage);
    }

    [Fact]
    public void AdjustedPhysicalDefenseAddsTortoiseGemPercent()
    {
        var defense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            physicalDefense: 29_051,
            tortoiseGemDamagePercent: 10);

        Assert.Equal(31_956U, defense);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(500, 0, 500)]
    [InlineData(505, 3, 520)]
    public void AdjustedPhysicalDefenseKeepsLegacyIntegerPercentRules(
        uint physicalDefense,
        uint tortoiseGemDamagePercent,
        uint expectedDefense)
    {
        var defense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            physicalDefense,
            tortoiseGemDamagePercent);

        Assert.Equal(expectedDefense, defense);
    }

    [Fact]
    public void AdjustedPhysicalDefenseMirrorsSourceAjustDefenseStateStacking()
    {
        var defense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            physicalDefense: 999,
            tortoiseGemDamagePercent: 10,
            adjustments: new LegacyPhysicalDefenseAdjustments(
                MagicShieldOrDefensePotion: true,
                DragonSwing: true,
                DivineGuard: true));

        Assert.Equal(3_444U, defense);
    }

    [Fact]
    public void MonsterPhysicalDamageUsesSourceAdjustedDefenseStates()
    {
        var damage = LegacyCombatDamageCalculator.CalculateMonsterPhysicalDamage(
            monsterMinAttack: 4_000,
            monsterMaxAttack: 4_001,
            targetItemBless: 7,
            targetPhysicalDefense: 999,
            targetTortoiseGemDamagePercent: 10,
            targetDefenseAdjustments: new LegacyPhysicalDefenseAdjustments(
                MagicShieldOrDefensePotion: true,
                DragonSwing: true,
                DivineGuard: true));

        Assert.Equal(276U, damage);
    }

    [Fact]
    public void MonsterPhysicalDamageConsumesRuntimeStatusDefenseAndDamageFields()
    {
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.ItemBlessIndex] = 20;
        fields[LegacyPlayerStatusPacket.DefenceIndex] = 1_000;
        fields[LegacyPlayerStatusPacket.DamageIndex] = 10;

        var damage = LegacyCombatDamageCalculator.CalculateMonsterPhysicalDamage(
            monsterMinAttack: 1_500,
            monsterMaxAttack: 2_000,
            targetItemBless: fields[LegacyPlayerStatusPacket.ItemBlessIndex],
            targetPhysicalDefense: fields[LegacyPlayerStatusPacket.DefenceIndex],
            targetTortoiseGemDamagePercent: fields[LegacyPlayerStatusPacket.DamageIndex]);

        Assert.Equal(100U, damage);
    }

    [Fact]
    public void CombatGearDamageInputsProjectLegacyStatusFieldsForCombatCalculators()
    {
        var fields = new uint[LegacyPlayerStatusPacket.FieldCount];
        fields[LegacyPlayerStatusPacket.MinAttackIndex] = 1_000;
        fields[LegacyPlayerStatusPacket.MaxAttackIndex] = 2_000;
        fields[LegacyPlayerStatusPacket.PhysicalPercentIndex] = 15;
        fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex] = 300;
        fields[LegacyPlayerStatusPacket.DefenceIndex] = 500;
        fields[LegacyPlayerStatusPacket.DamageIndex] = 10;
        fields[LegacyPlayerStatusPacket.ItemBlessIndex] = 7;
        fields[LegacyPlayerStatusPacket.DodgeIndex] = 30;

        var inputs = LegacyCombatGearDamageInputs.FromStatusFields(fields);

        Assert.Equal(1_000U, inputs.MinAttack);
        Assert.Equal(2_000U, inputs.MaxAttack);
        Assert.Equal(15U, inputs.PhysicalPercent);
        Assert.Equal(300U, inputs.PhysicalDamageIncrease);
        Assert.Equal(500U, inputs.PhysicalDefense);
        Assert.Equal(10U, inputs.TortoiseGemDamagePercent);
        Assert.Equal(550U, inputs.AdjustedPhysicalDefense);
        Assert.Equal(7U, inputs.ItemBless);
        Assert.Equal(30U, inputs.Dodge);
    }

    [Fact]
    public void MonsterPhysicalDamageUsesMaxAttackWhenMinAttackIsMissing()
    {
        var damage = LegacyCombatDamageCalculator.CalculateMonsterPhysicalDamage(
            monsterMinAttack: 0,
            monsterMaxAttack: 400,
            targetItemBless: 0,
            targetPhysicalDefense: 150);

        Assert.Equal(250U, damage);
    }

    [Fact]
    public void MinimumUserMonsterDamageIncludesLegacyWeaponTailDigits()
    {
        var damage = LegacyCombatDamageCalculator.CalculateMinimumUserMonsterDamage(
            playerLevel: 130,
            equipmentItems:
            [
                new PlayerItemSnapshotItem { ItemId = 500_329, Position = 4 },
                new PlayerItemSnapshotItem { ItemId = 421_335, Position = 5 },
                new PlayerItemSnapshotItem { ItemId = 120_269, Position = 3 },
                new PlayerItemSnapshotItem { ItemId = 601_339, Position = 54 },
            ]);

        Assert.Equal(28U, damage);
    }
}
