namespace WolfConquer.Core.World;

public static class LegacyMonsterPlayerPhysicalDamageCalculator
{
    public const uint BossMinimumDamage = 3_000;
    public const uint BossMaximumExclusiveDamage = 6_500;

    public static uint CalculateFromSelectedAttack(
        uint selectedAttack,
        uint playerAdjustedDefense,
        uint playerBlessPercent,
        uint physicalDamagePercent = 0,
        uint playerDodge = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null)
    {
        ValidateDodgeRolls(firstDodgeRoll, secondDodgeRoll);

        var damage = AddLegacyPercentBonus(selectedAttack, physicalDamagePercent);
        damage = LegacyCombatDamageCalculator.DecreaseLegacyBless(damage, playerBlessPercent);
        damage = damage <= playerAdjustedDefense ? 1 : damage - playerAdjustedDefense;
        damage = ApplyBossMinimumDamage(damage, selectedBossMinimumDamage);

        if (firstDodgeRoll.HasValue && !CheckLegacyDodgeHit(playerDodge, firstDodgeRoll.Value, secondDodgeRoll))
        {
            return 0;
        }

        return damage;
    }

    public static uint CalculateFromSelectedAttackWithDefense(
        uint selectedAttack,
        uint playerPhysicalDefense,
        uint playerBlessPercent,
        uint playerTortoiseGemDamagePercent = 0,
        uint physicalDamagePercent = 0,
        uint playerDodge = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null,
        LegacyPhysicalDefenseAdjustments playerDefenseAdjustments = default)
    {
        var adjustedDefense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            playerPhysicalDefense,
            playerTortoiseGemDamagePercent,
            playerDefenseAdjustments);

        return CalculateFromSelectedAttack(
            selectedAttack,
            adjustedDefense,
            playerBlessPercent,
            physicalDamagePercent,
            playerDodge,
            firstDodgeRoll,
            secondDodgeRoll,
            selectedBossMinimumDamage);
    }

    public static uint CalculateFromSelectedAttackWithStatusFields(
        uint selectedAttack,
        IReadOnlyList<uint> targetStatusFields,
        uint minimumAdjustedPhysicalDefense = 0,
        uint physicalDamagePercent = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null,
        LegacyPhysicalDefenseAdjustments playerDefenseAdjustments = default)
    {
        return CalculateFromSelectedAttackWithStatusInputs(
            selectedAttack,
            LegacyCombatGearDamageInputs.FromStatusFields(
                targetStatusFields,
                minimumAdjustedPhysicalDefense,
                playerDefenseAdjustments),
            physicalDamagePercent,
            firstDodgeRoll,
            secondDodgeRoll,
            selectedBossMinimumDamage);
    }

    public static uint CalculateFromSelectedAttackWithStatusInputs(
        uint selectedAttack,
        LegacyCombatGearDamageInputs targetStatus,
        uint physicalDamagePercent = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null)
    {
        return CalculateFromSelectedAttack(
            selectedAttack,
            targetStatus.AdjustedPhysicalDefense,
            targetStatus.ItemBless,
            physicalDamagePercent,
            targetStatus.Dodge,
            firstDodgeRoll,
            secondDodgeRoll,
            selectedBossMinimumDamage);
    }

    public static uint CalculateFromAttackRange(
        uint monsterMinAttack,
        uint monsterMaxAttack,
        uint attackRoll,
        uint playerAdjustedDefense,
        uint playerBlessPercent,
        uint physicalDamagePercent = 0,
        uint playerDodge = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null)
    {
        var selectedAttack = SelectLegacyAttack(monsterMinAttack, monsterMaxAttack, attackRoll);

        return CalculateFromSelectedAttack(
            selectedAttack,
            playerAdjustedDefense,
            playerBlessPercent,
            physicalDamagePercent,
            playerDodge,
            firstDodgeRoll,
            secondDodgeRoll,
            selectedBossMinimumDamage);
    }

    public static uint CalculateFromAttackRangeWithDefense(
        uint monsterMinAttack,
        uint monsterMaxAttack,
        uint attackRoll,
        uint playerPhysicalDefense,
        uint playerBlessPercent,
        uint playerTortoiseGemDamagePercent = 0,
        uint physicalDamagePercent = 0,
        uint playerDodge = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null,
        LegacyPhysicalDefenseAdjustments playerDefenseAdjustments = default)
    {
        var adjustedDefense = LegacyCombatDamageCalculator.CalculateAdjustedPhysicalDefense(
            playerPhysicalDefense,
            playerTortoiseGemDamagePercent,
            playerDefenseAdjustments);

        return CalculateFromAttackRange(
            monsterMinAttack,
            monsterMaxAttack,
            attackRoll,
            adjustedDefense,
            playerBlessPercent,
            physicalDamagePercent,
            playerDodge,
            firstDodgeRoll,
            secondDodgeRoll,
            selectedBossMinimumDamage);
    }

    public static uint CalculateFromAttackRangeWithStatusFields(
        uint monsterMinAttack,
        uint monsterMaxAttack,
        uint attackRoll,
        IReadOnlyList<uint> targetStatusFields,
        uint minimumAdjustedPhysicalDefense = 0,
        uint physicalDamagePercent = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null,
        LegacyPhysicalDefenseAdjustments playerDefenseAdjustments = default)
    {
        return CalculateFromAttackRangeWithStatusInputs(
            monsterMinAttack,
            monsterMaxAttack,
            attackRoll,
            LegacyCombatGearDamageInputs.FromStatusFields(
                targetStatusFields,
                minimumAdjustedPhysicalDefense,
                playerDefenseAdjustments),
            physicalDamagePercent,
            firstDodgeRoll,
            secondDodgeRoll,
            selectedBossMinimumDamage);
    }

    public static uint CalculateFromAttackRangeWithStatusInputs(
        uint monsterMinAttack,
        uint monsterMaxAttack,
        uint attackRoll,
        LegacyCombatGearDamageInputs targetStatus,
        uint physicalDamagePercent = 0,
        uint? firstDodgeRoll = null,
        uint? secondDodgeRoll = null,
        uint? selectedBossMinimumDamage = null)
    {
        var selectedAttack = SelectLegacyAttack(monsterMinAttack, monsterMaxAttack, attackRoll);

        return CalculateFromSelectedAttackWithStatusInputs(
            selectedAttack,
            targetStatus,
            physicalDamagePercent,
            firstDodgeRoll,
            secondDodgeRoll,
            selectedBossMinimumDamage);
    }

    public static uint SelectLegacyAttack(uint monsterMinAttack, uint monsterMaxAttack, uint attackRoll)
    {
        var lower = Math.Min(monsterMinAttack, monsterMaxAttack);
        var upper = Math.Max(monsterMinAttack, monsterMaxAttack);
        if (lower == upper)
        {
            return lower;
        }

        var width = upper - lower;
        return lower + attackRoll % width;
    }

    public static bool CheckLegacyDodgeHit(uint playerDodge, uint firstDodgeRoll, uint? secondDodgeRoll = null)
    {
        var roll = firstDodgeRoll % 150;
        if (roll > 60)
        {
            if (!secondDodgeRoll.HasValue)
            {
                throw new ArgumentException(
                    "A second dodge roll is required when the first legacy dodge roll is greater than 60.",
                    nameof(secondDodgeRoll));
            }

            roll = secondDodgeRoll.Value % 150;
        }

        return roll >= playerDodge;
    }

    public static uint SelectBossMinimumDamage(uint bossMinimumDamageRoll)
    {
        return BossMinimumDamage + bossMinimumDamageRoll % (BossMaximumExclusiveDamage - BossMinimumDamage);
    }

    private static uint AddLegacyPercentBonus(uint damage, uint percent)
    {
        if (damage == 0 || percent == 0)
        {
            return damage;
        }

        var bonus = checked((uint)(((ulong)damage * percent) / 100));
        return checked(damage + bonus);
    }

    private static uint ApplyBossMinimumDamage(uint damage, uint? selectedBossMinimumDamage)
    {
        if (!selectedBossMinimumDamage.HasValue || damage >= BossMinimumDamage)
        {
            return damage;
        }

        if (selectedBossMinimumDamage.Value < BossMinimumDamage ||
            selectedBossMinimumDamage.Value >= BossMaximumExclusiveDamage)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selectedBossMinimumDamage),
                selectedBossMinimumDamage.Value,
                "Legacy boss physical minimum damage must be in the [3000, 6500) range.");
        }

        return selectedBossMinimumDamage.Value;
    }

    private static void ValidateDodgeRolls(uint? firstDodgeRoll, uint? secondDodgeRoll)
    {
        if (secondDodgeRoll.HasValue && !firstDodgeRoll.HasValue)
        {
            throw new ArgumentException(
                "A first dodge roll is required when a second legacy dodge roll is provided.",
                nameof(firstDodgeRoll));
        }
    }
}
