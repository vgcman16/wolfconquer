namespace WolfConquer.Core.World;

public enum LegacyPlayerMonsterPhysicalAttackRoll
{
    MinimumRoll,
    AverageRoll,
    MaximumRoll,
}

public static class LegacyPlayerMonsterPhysicalDamageCalculator
{
    public readonly record struct PostDefense2EffectResult(
        uint Damage,
        MsgAttackEffect Effect);

    public static uint Calculate(
        uint minAttack,
        uint maxAttack,
        uint monsterDefense,
        uint monsterDefense2,
        int attackerLevel,
        int monsterLevel,
        uint finalPhysicalIncreasePercent = 0,
        uint finalPhysicalDamageIncrease = 0,
        bool isGuard = false,
        bool forceOneDamage = false,
        LegacyPlayerMonsterPhysicalAttackRoll attackRoll = LegacyPlayerMonsterPhysicalAttackRoll.AverageRoll,
        uint? deterministicRoll = null,
        uint physicalSpellDamagePercent = 100,
        uint monsterDamageBonusPercent = 0,
        bool hasSuperman = false,
        bool hasFatalStrike = false,
        bool hasManiacDance = false,
        bool hasOblivion = false,
        bool isBoss = false,
        bool hasStigmaOrAttackPotion = false,
        uint focusDamagePercent = 0,
        uint extraMaxAttackBonus = 0)
    {
        var damage = SelectAttack(minAttack, maxAttack, attackRoll, deterministicRoll);
        damage = ApplySourceAjustAttack(
            damage,
            finalPhysicalIncreasePercent,
            hasStigmaOrAttackPotion,
            focusDamagePercent);
        damage = LegacyCombatDamageCalculator.AddLegacySaturating(damage, extraMaxAttackBonus);
        damage = damage <= monsterDefense ? 0 : damage - monsterDefense;
        damage = ApplySourcePhysicalStatusMultipliers(
            damage,
            physicalSpellDamagePercent,
            monsterDamageBonusPercent,
            hasSuperman,
            hasFatalStrike,
            hasManiacDance,
            hasOblivion,
            isBoss);

        damage = Math.Max(damage, CalculateMinimumUserMonsterDamage(attackerLevel));
        damage = CalculateUserMonsterLevelDamage(damage, monsterDefense, attackerLevel, monsterLevel);
        damage = BigMulDivToUInt32(damage, monsterDefense2, 10_000);
        if (monsterDefense2 > 0)
        {
            damage = LegacyCombatDamageCalculator.CalculateLegacyExtraAttack(
                damage,
                finalPhysicalDamageIncrease,
                defenseDecrease: 0);
            damage = Math.Max(1, damage);
        }

        if (isGuard)
        {
            damage /= 10;
        }

        return forceOneDamage ? 1 : damage;
    }

    public static uint ApplySourceAjustAttack(
        uint damage,
        uint physicalPercent,
        bool hasStigmaOrAttackPotion = false,
        uint focusDamagePercent = 0)
    {
        var bonus = 0UL;
        if (hasStigmaOrAttackPotion)
        {
            bonus += CalculateSourcePercentDelta(damage, 130);
        }

        if (physicalPercent > 0)
        {
            bonus += ((ulong)damage * physicalPercent) / 100UL;
        }

        if (focusDamagePercent > 0)
        {
            bonus += CalculateSourcePercentDelta(damage, focusDamagePercent);
        }

        return LegacyCombatDamageCalculator.AddLegacySaturating(
            damage,
            bonus > uint.MaxValue ? uint.MaxValue : (uint)bonus);
    }

    public static PostDefense2EffectResult ApplySourcePostDefense2Effects(
        uint damage,
        uint luckyStrikeRatePercent,
        uint criticalStrike,
        uint dashRate,
        uint luckyStrikeRoll,
        uint refineryRoll,
        uint dashRoll,
        uint whirlSigilSageDamagePercent = 0)
    {
        var effect = MsgAttackEffect.None;
        if (CheckSourceRate(luckyStrikeRatePercent, luckyStrikeRoll))
        {
            effect |= MsgAttackEffect.LuckyStrike;
            damage = MulDivToUInt32(damage, 200, 100);
        }

        if (CheckSourceRate(40, refineryRoll))
        {
            if (criticalStrike != 0)
            {
                effect |= MsgAttackEffect.CriticalStrike;
                damage = AddPostDefense2Bonus(
                    damage,
                    criticalStrikeDivisor: 150,
                    criticalStrike,
                    whirlSigilSageDamagePercent);
            }
        }
        else if (CheckSourceRate(dashRate / 100, dashRoll))
        {
            effect |= MsgAttackEffect.Dash;
            damage = AddPostDefense2Bonus(
                damage,
                criticalStrikeDivisor: 100,
                criticalStrike,
                whirlSigilSageDamagePercent);
        }

        return new PostDefense2EffectResult(damage, effect);
    }

    public static uint ApplySourceKylinSigilDamage(uint damage, uint kylinSigil)
    {
        var bonusPercent = kylinSigil switch
        {
            1 => 50U,
            2 => 60U,
            3 => 75U,
            _ => 0U,
        };

        if (bonusPercent == 0 || damage == 0)
        {
            return damage;
        }

        var result = (ulong)damage + ((ulong)damage * bonusPercent / 100UL);
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    public static uint ApplySourceAssassinAutoHuntRuneBonus(
        uint damage,
        bool hasAssassinRune,
        bool onAutoHunt)
    {
        if (!hasAssassinRune || !onAutoHunt)
        {
            return damage;
        }

        var result = (ulong)damage + 38_000UL;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    public static uint ApplySourceDeathSighMonsterPhysicalDamage(
        uint damage,
        bool hasDeathSigh,
        bool activeSpell,
        uint passivePercent,
        uint activePercent)
    {
        if (!hasDeathSigh)
        {
            return damage;
        }

        if (!activeSpell)
        {
            return MulDivToUInt32(damage, passivePercent, 100);
        }

        var result = (ulong)damage + ((ulong)damage * activePercent / 100UL);
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    public static uint ApplySourceInfinityMonsterPhysicalDamage(
        uint damage,
        bool hasInfinity,
        bool spellDamage,
        uint monsterDamagePercent,
        uint spellDamagePercent)
    {
        if (!hasInfinity)
        {
            return damage;
        }

        return MulDivToUInt32(damage, spellDamage ? spellDamagePercent : monsterDamagePercent, 100);
    }

    public static uint GetNextSourceKylinSigil(uint currentKylinSigil)
    {
        return currentKylinSigil == 3 ? 1U : Math.Min(currentKylinSigil + 1U, 3U);
    }

    public static uint ApplySourcePhysicalStatusMultipliers(
        uint damage,
        uint physicalSpellDamagePercent = 100,
        uint monsterDamageBonusPercent = 0,
        bool hasSuperman = false,
        bool hasFatalStrike = false,
        bool hasManiacDance = false,
        bool hasOblivion = false,
        bool isBoss = false)
    {
        if (hasSuperman)
        {
            damage = LegacyCombatDamageCalculator.AddLegacySaturating(
                damage,
                LegacyCombatDamageCalculator.AddLegacySaturating(damage, damage));
        }

        damage = hasFatalStrike
            ? MulDivToUInt32(damage, 500, 100)
            : MulDivToUInt32(
                damage,
                LegacyCombatDamageCalculator.AddLegacySaturating(
                    physicalSpellDamagePercent,
                    monsterDamageBonusPercent),
                100);

        if (hasManiacDance)
        {
            damage = MulDivToUInt32(damage, isBoss ? 3U : 10U, 1);
        }

        if (hasOblivion)
        {
            damage = MulDivToUInt32(damage, 200, 100);
        }

        return damage;
    }

    public static uint CalculateFromStatusFields(
        IReadOnlyList<uint> attackerStatusFields,
        uint monsterDefense,
        uint monsterDefense2,
        int attackerLevel,
        int monsterLevel,
        bool isGuard = false,
        bool forceOneDamage = false,
        LegacyPlayerMonsterPhysicalAttackRoll attackRoll = LegacyPlayerMonsterPhysicalAttackRoll.AverageRoll,
        uint? deterministicRoll = null)
    {
        return CalculateFromStatusInputs(
            LegacyCombatGearDamageInputs.FromStatusFields(attackerStatusFields),
            monsterDefense,
            monsterDefense2,
            attackerLevel,
            monsterLevel,
            isGuard,
            forceOneDamage,
            attackRoll,
            deterministicRoll);
    }

    public static uint CalculateFromStatusInputs(
        LegacyCombatGearDamageInputs attackerStatus,
        uint monsterDefense,
        uint monsterDefense2,
        int attackerLevel,
        int monsterLevel,
        bool isGuard = false,
        bool forceOneDamage = false,
        LegacyPlayerMonsterPhysicalAttackRoll attackRoll = LegacyPlayerMonsterPhysicalAttackRoll.AverageRoll,
        uint? deterministicRoll = null)
    {
        return Calculate(
            attackerStatus.MinAttack,
            attackerStatus.MaxAttack,
            monsterDefense,
            monsterDefense2,
            attackerLevel,
            monsterLevel,
            attackerStatus.PhysicalPercent,
            attackerStatus.PhysicalDamageIncrease,
            isGuard,
            forceOneDamage,
            attackRoll,
            deterministicRoll);
    }

    public static uint SelectAttack(
        uint minAttack,
        uint maxAttack,
        LegacyPlayerMonsterPhysicalAttackRoll attackRoll = LegacyPlayerMonsterPhysicalAttackRoll.AverageRoll,
        uint? deterministicRoll = null)
    {
        if (maxAttack <= minAttack)
        {
            maxAttack = minAttack == uint.MaxValue ? uint.MaxValue : minAttack + 1;
        }

        var range = maxAttack - minAttack;
        if (range == 0)
        {
            return minAttack;
        }

        var roll = deterministicRoll ?? attackRoll switch
        {
            LegacyPlayerMonsterPhysicalAttackRoll.MinimumRoll => 0,
            LegacyPlayerMonsterPhysicalAttackRoll.MaximumRoll => range - 1,
            _ => (range - 1) / 2,
        };

        roll = Math.Min(roll, range - 1);
        return LegacyCombatDamageCalculator.AddLegacySaturating(minAttack, roll / 2);
    }

    private static ulong CalculateSourcePercentDelta(uint damage, uint percent)
    {
        var adjusted = ((ulong)damage * percent) / 100UL;
        return adjusted <= damage ? 0 : adjusted - damage;
    }

    private static uint CalculateMinimumUserMonsterDamage(int attackerLevel)
    {
        return checked((uint)(1 + (Math.Max(0, attackerLevel) / 10)));
    }

    private static uint CalculateUserMonsterLevelDamage(
        uint attack,
        uint defense,
        int attackerLevel,
        int monsterLevel)
    {
        if (GetMonsterNameType(attackerLevel, monsterLevel) > 2)
        {
            return attack <= defense ? 0 : attack - defense;
        }

        var levelDelta = attackerLevel - monsterLevel;
        var scaled = attack;
        if (levelDelta >= 0 && levelDelta < 3)
        {
            scaled = MulDivToUInt32(attack, 120, 100);
        }
        else if (levelDelta >= 3 && levelDelta <= 5)
        {
            scaled = MulDivToUInt32(attack, 150, 100);
        }
        else if (levelDelta > 5 && levelDelta <= 10)
        {
            scaled = MulDivToUInt32(attack, 200, 100);
        }
        else if (levelDelta > 10 && levelDelta <= 20)
        {
            scaled = MulDivToUInt32(attack, 250, 100);
        }
        else if (levelDelta > 20)
        {
            scaled = MulDivToUInt32(attack, 300, 100);
        }

        return scaled <= defense ? 0 : scaled - defense;
    }

    private static int GetMonsterNameType(int attackerLevel, int monsterLevel)
    {
        var levelDelta = attackerLevel - monsterLevel;
        if (levelDelta >= 3)
        {
            return 1;
        }

        if (levelDelta >= 0)
        {
            return 2;
        }

        return levelDelta >= -5 ? 3 : 4;
    }

    private static uint MulDivToUInt32(uint number, uint numerator, uint denominator)
    {
        if (denominator == 0)
        {
            return uint.MaxValue;
        }

        var result = (ulong)number * numerator / denominator;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    private static bool CheckSourceRate(uint value, uint roll)
    {
        return value > roll % 100;
    }

    private static uint AddPostDefense2Bonus(
        uint damage,
        uint criticalStrikeDivisor,
        uint criticalStrike,
        uint whirlSigilSageDamagePercent)
    {
        if (damage == 0)
        {
            return 0;
        }

        var criticalBonus = (ulong)damage * (criticalStrike / 100UL) / criticalStrikeDivisor;
        var result = (ulong)damage + criticalBonus;
        result += result * whirlSigilSageDamagePercent / 100UL;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }

    private static uint BigMulDivToUInt32(uint number, uint numerator, uint denominator)
    {
        if (denominator == 0)
        {
            return uint.MaxValue;
        }

        var result = ((ulong)number * numerator + (denominator / 2UL)) / denominator;
        return result > uint.MaxValue ? uint.MaxValue : (uint)result;
    }
}
