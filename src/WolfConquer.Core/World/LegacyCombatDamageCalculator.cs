using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public readonly record struct LegacyPhysicalDefenseAdjustments(
    bool MagicShieldOrDefensePotion = false,
    bool DragonSwing = false,
    bool DivineGuard = false,
    uint? HookMoonAttackedPowerPercent = null);

public static class LegacyCombatDamageCalculator
{
    private const uint MagicShieldDefensePercentBonus = 20;
    private const uint DragonSwingDefensePercentBonus = 15;
    private const uint DivineGuardDefensePercentBonus = 200;

    public static uint CalculateMonsterPhysicalDamage(
        uint monsterMinAttack,
        uint monsterMaxAttack,
        uint targetItemBless,
        uint targetPhysicalDefense,
        uint targetTortoiseGemDamagePercent = 0,
        LegacyPhysicalDefenseAdjustments targetDefenseAdjustments = default)
    {
        var damage = monsterMinAttack == 0 && monsterMaxAttack == 0
            ? 1
            : Math.Max(1, monsterMinAttack == 0 ? monsterMaxAttack : monsterMinAttack);

        damage = DecreaseLegacyBless(damage, targetItemBless);
        var adjustedDefense = CalculateAdjustedPhysicalDefense(
            targetPhysicalDefense,
            targetTortoiseGemDamagePercent,
            targetDefenseAdjustments);
        return damage <= adjustedDefense ? 1 : damage - adjustedDefense;
    }

    public static uint DecreaseLegacyBless(uint damage, uint bless)
    {
        if (damage == 0 || bless == 0)
        {
            return damage;
        }

        var decrease = checked((uint)(((ulong)damage * bless) / 100));
        return damage <= decrease ? 1 : damage - decrease;
    }

    public static uint CalculateLegacyExtraAttack(uint damage, uint attackIncrease, uint defenseDecrease)
    {
        damage = AddLegacySaturating(damage, attackIncrease);
        return damage <= defenseDecrease ? 1 : damage - defenseDecrease;
    }

    public static uint AddLegacyPercentBonus(uint damage, uint percent)
    {
        if (damage == 0 || percent == 0)
        {
            return damage;
        }

        var bonus = ((ulong)damage * percent) / 100;
        return AddLegacySaturating(damage, bonus > uint.MaxValue ? uint.MaxValue : (uint)bonus);
    }

    public static uint AddLegacySaturating(uint left, uint right)
    {
        var sum = (ulong)left + right;
        return sum > uint.MaxValue ? uint.MaxValue : (uint)sum;
    }

    public static uint CalculateAdjustedPhysicalDefense(
        uint physicalDefense,
        uint tortoiseGemDamagePercent,
        LegacyPhysicalDefenseAdjustments adjustments = default)
    {
        if (physicalDefense == 0)
        {
            return physicalDefense;
        }

        var bonus = CalculateBaseDefensePercent(physicalDefense, tortoiseGemDamagePercent);
        if (adjustments.MagicShieldOrDefensePotion)
        {
            bonus = checked(bonus + CalculateBaseDefensePercent(physicalDefense, MagicShieldDefensePercentBonus));
        }

        if (adjustments.DragonSwing)
        {
            bonus = checked(bonus + CalculateBaseDefensePercent(physicalDefense, DragonSwingDefensePercentBonus));
        }

        if (adjustments.DivineGuard)
        {
            bonus = checked(bonus + CalculateBaseDefensePercent(physicalDefense, DivineGuardDefensePercentBonus));
        }

        var adjustedDefense = checked(physicalDefense + bonus);
        if (adjustments.HookMoonAttackedPowerPercent.HasValue)
        {
            adjustedDefense = checked((uint)(((ulong)adjustedDefense * adjustments.HookMoonAttackedPowerPercent.Value) / 100));
        }

        return adjustedDefense;
    }

    public static uint CalculateMinimumUserMonsterDamage(
        uint playerLevel,
        IReadOnlyList<PlayerItemSnapshotItem> equipmentItems)
    {
        var minimum = checked(1U + playerLevel / 10U);

        foreach (var item in equipmentItems)
        {
            if (item.Position is 4 or 5)
            {
                minimum = checked(minimum + item.ItemId % 10U);
            }
        }

        return Math.Max(1, minimum);
    }

    private static uint CalculateBaseDefensePercent(uint physicalDefense, uint percent)
    {
        if (percent == 0)
        {
            return 0;
        }

        return checked((uint)(((ulong)physicalDefense * percent) / 100));
    }
}
