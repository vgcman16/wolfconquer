using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerMonsterPhysicalDamageCalculatorTests
{
    [Fact]
    public void HighGearAttackIncreasesDamageAboveLowGear()
    {
        var lowGearDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 130,
            monsterLevel: 120);
        var highGearDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 4_000,
            maxAttack: 4_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 130,
            monsterLevel: 120);

        Assert.True(highGearDamage > lowGearDamage);
    }

    [Fact]
    public void StatusFieldsApplyDragonPercentAndFinalPhysicalDamageIncreaseToMonsterDamage()
    {
        var lowGearFields = new uint[LegacyPlayerStatusPacket.FieldCount];
        lowGearFields[LegacyPlayerStatusPacket.MinAttackIndex] = 1_000;
        lowGearFields[LegacyPlayerStatusPacket.MaxAttackIndex] = 1_000;

        var highGearFields = (uint[])lowGearFields.Clone();
        highGearFields[LegacyPlayerStatusPacket.PhysicalPercentIndex] = 15;
        highGearFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex] = 3_000;

        var lowGearDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            lowGearFields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120);
        var highGearDamage = LegacyPlayerMonsterPhysicalDamageCalculator.CalculateFromStatusFields(
            highGearFields,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120);

        Assert.Equal(980U, lowGearDamage);
        // Source: Physical.OnMonster applies AjustAttack, then CalculateExtraAttack after Defense2.
        Assert.Equal(4_160U, highGearDamage);
        Assert.True(highGearDamage > lowGearDamage);
    }

    [Fact]
    public void MonsterDefenseReducesDamage()
    {
        var lowDefenseDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120);
        var highDefenseDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 400,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120);

        Assert.Equal(980U, lowDefenseDamage);
        Assert.Equal(320U, highDefenseDamage);
        Assert.True(highDefenseDamage < lowDefenseDamage);
    }

    [Fact]
    public void Defense2ScalesDamageDownWithLegacyRoundedBigMulDiv()
    {
        var fullDefense2Damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 3_000,
            maxAttack: 3_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120);
        var halfDefense2Damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 3_000,
            maxAttack: 3_000,
            monsterDefense: 100,
            monsterDefense2: 5_000,
            attackerLevel: 120,
            monsterLevel: 120);

        Assert.Equal(3_380U, fullDefense2Damage);
        Assert.Equal(1_690U, halfDefense2Damage);
    }

    [Fact]
    public void LevelGapIncreasesDamageWhenPlayerOutlevelsMonster()
    {
        var lowerLevelDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 130);
        var higherLevelDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 130,
            monsterLevel: 120);

        Assert.Equal(800U, lowerLevelDamage);
        Assert.Equal(1_700U, higherLevelDamage);
        Assert.True(higherLevelDamage > lowerLevelDamage);
    }

    [Fact]
    public void FinalPhysicalIncreasePercentUsesLegacyAjustAttackDirection()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            finalPhysicalIncreasePercent: 50);

        Assert.Equal(1_580U, damage);
    }

    [Fact]
    public void SourceAjustAttackAndAjustMaxAttackRaiseMonsterDamageBeforeDefense()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            finalPhysicalIncreasePercent: 15,
            hasStigmaOrAttackPotion: true,
            focusDamagePercent: 200,
            extraMaxAttackBonus: 500);

        // Source: AjustAttack sums Stigma, PhysicalPercent, and Focus deltas from the same rolled damage;
        // then AjustMaxAttack adds ExtraStatus IncreasePAttack before monster defense is applied.
        Assert.Equal(3_320U, damage);
    }

    [Fact]
    public void SupermanTriplesPhysicalMonsterDamageBeforeLevelScaling()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            hasSuperman: true);

        Assert.Equal(3_140U, damage);
    }

    [Fact]
    public void FatalStrikeUsesSourceFiveTimesMonsterDamage()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            hasFatalStrike: true);

        Assert.Equal(5_300U, damage);
    }

    [Fact]
    public void FatalStrikeOverridesPhysicalSpellDamagePercent()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            physicalSpellDamagePercent: 350,
            hasFatalStrike: true);

        Assert.Equal(5_300U, damage);
    }

    [Fact]
    public void MonsterDamageBonusAddsToPhysicalMonsterSpellPercent()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            physicalSpellDamagePercent: 120,
            monsterDamageBonusPercent: 300);

        // Source: BossKiller/MonsterHunter append IncDamagee to DBSpell.Damage before /100 scaling.
        Assert.Equal(4_436U, damage);
    }

    [Fact]
    public void ManiacDanceUsesTenTimesDamageAgainstNonBossMonsters()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            hasManiacDance: true);

        Assert.Equal(10_700U, damage);
    }

    [Fact]
    public void ManiacDanceUsesThreeTimesDamageAgainstBossMonsters()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            hasManiacDance: true,
            isBoss: true);

        Assert.Equal(3_140U, damage);
    }

    [Fact]
    public void OblivionDoublesPhysicalMonsterDamageBeforeLevelScaling()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            hasOblivion: true);

        Assert.Equal(2_060U, damage);
    }

    [Fact]
    public void GuardFlagDividesDamageByTenAfterDefense2Floor()
    {
        var normalDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 3_000,
            maxAttack: 3_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120);
        var guardDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 3_000,
            maxAttack: 3_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            isGuard: true);

        Assert.Equal(338U, guardDamage);
        Assert.Equal(normalDamage / 10, guardDamage);
    }

    [Fact]
    public void PostDefense2EffectsApplyLuckyStrikeBeforeCriticalStrike()
    {
        var result = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            damage: 980,
            luckyStrikeRatePercent: 10,
            criticalStrike: 15_000,
            dashRate: 0,
            luckyStrikeRoll: 9,
            refineryRoll: 0,
            dashRoll: 99);

        Assert.Equal(3_920U, result.Damage);
        Assert.Equal(
            MsgAttackEffect.LuckyStrike | MsgAttackEffect.CriticalStrike,
            result.Effect);
    }

    [Fact]
    public void PostDefense2EffectsUseDashOnlyWhenRefineryRollMisses()
    {
        var refineryHit = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            damage: 980,
            luckyStrikeRatePercent: 0,
            criticalStrike: 10_000,
            dashRate: 10_000,
            luckyStrikeRoll: 99,
            refineryRoll: 39,
            dashRoll: 0);
        var refineryMiss = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            damage: 980,
            luckyStrikeRatePercent: 0,
            criticalStrike: 10_000,
            dashRate: 10_000,
            luckyStrikeRoll: 99,
            refineryRoll: 40,
            dashRoll: 0);

        Assert.Equal(1_633U, refineryHit.Damage);
        Assert.Equal(MsgAttackEffect.CriticalStrike, refineryHit.Effect);
        Assert.Equal(1_960U, refineryMiss.Damage);
        Assert.Equal(MsgAttackEffect.Dash, refineryMiss.Effect);
    }

    [Fact]
    public void PostDefense2EffectsApplyWhirlSigilPercentAfterCriticalStrikeBonus()
    {
        var result = LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourcePostDefense2Effects(
            damage: 1_000,
            luckyStrikeRatePercent: 0,
            criticalStrike: 15_000,
            dashRate: 0,
            luckyStrikeRoll: 99,
            refineryRoll: 0,
            dashRoll: 99,
            whirlSigilSageDamagePercent: 25);

        Assert.Equal(2_500U, result.Damage);
        Assert.Equal(MsgAttackEffect.CriticalStrike, result.Effect);
    }

    [Fact]
    public void KylinSigilDamageUsesSourceBonusCycle()
    {
        Assert.Equal(1_500U, LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceKylinSigilDamage(1_000, 1));
        Assert.Equal(1_600U, LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceKylinSigilDamage(1_000, 2));
        Assert.Equal(1_750U, LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceKylinSigilDamage(1_000, 3));
    }

    [Fact]
    public void KylinSigilStateWrapsAfterThirdHit()
    {
        Assert.Equal(1U, LegacyPlayerMonsterPhysicalDamageCalculator.GetNextSourceKylinSigil(0));
        Assert.Equal(2U, LegacyPlayerMonsterPhysicalDamageCalculator.GetNextSourceKylinSigil(1));
        Assert.Equal(3U, LegacyPlayerMonsterPhysicalDamageCalculator.GetNextSourceKylinSigil(2));
        Assert.Equal(1U, LegacyPlayerMonsterPhysicalDamageCalculator.GetNextSourceKylinSigil(3));
    }

    [Fact]
    public void AssassinAutoHuntRuneBonusRequiresRuneAndAutoHunt()
    {
        Assert.Equal(
            39_000U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceAssassinAutoHuntRuneBonus(
                1_000,
                hasAssassinRune: true,
                onAutoHunt: true));
        Assert.Equal(
            1_000U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceAssassinAutoHuntRuneBonus(
                1_000,
                hasAssassinRune: false,
                onAutoHunt: true));
        Assert.Equal(
            1_000U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceAssassinAutoHuntRuneBonus(
                1_000,
                hasAssassinRune: true,
                onAutoHunt: false));
    }

    [Fact]
    public void DeathSighMonsterPhysicalDamageMatchesSourcePassiveAndActiveBranches()
    {
        Assert.Equal(
            1_500U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceDeathSighMonsterPhysicalDamage(
                1_000,
                hasDeathSigh: true,
                activeSpell: false,
                passivePercent: 150,
                activePercent: 40));
        Assert.Equal(
            1_400U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceDeathSighMonsterPhysicalDamage(
                1_000,
                hasDeathSigh: true,
                activeSpell: true,
                passivePercent: 150,
                activePercent: 40));
        Assert.Equal(
            1_000U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceDeathSighMonsterPhysicalDamage(
                1_000,
                hasDeathSigh: false,
                activeSpell: true,
                passivePercent: 150,
                activePercent: 40));
    }

    [Fact]
    public void InfinityMonsterPhysicalDamageUsesMonsterOrSpellPercentBySourceBranch()
    {
        Assert.Equal(
            1_750U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceInfinityMonsterPhysicalDamage(
                1_000,
                hasInfinity: true,
                spellDamage: false,
                monsterDamagePercent: 175,
                spellDamagePercent: 250));
        Assert.Equal(
            2_500U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceInfinityMonsterPhysicalDamage(
                1_000,
                hasInfinity: true,
                spellDamage: true,
                monsterDamagePercent: 175,
                spellDamagePercent: 250));
        Assert.Equal(
            1_000U,
            LegacyPlayerMonsterPhysicalDamageCalculator.ApplySourceInfinityMonsterPhysicalDamage(
                1_000,
                hasInfinity: false,
                spellDamage: true,
                monsterDamagePercent: 175,
                spellDamagePercent: 250));
    }

    [Fact]
    public void SpecialOneDamageMonsterClampOverridesNormalDamage()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 3_000,
            maxAttack: 3_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120,
            forceOneDamage: true);

        Assert.Equal(1U, damage);
    }

    [Fact]
    public void ServerMathCanExceedClientReportedPacketDamage()
    {
        const uint clientReportedPacketDamage = 1;

        var serverDamage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 3_000,
            maxAttack: 3_000,
            monsterDefense: 100,
            monsterDefense2: 10_000,
            attackerLevel: 120,
            monsterLevel: 120);

        Assert.True(serverDamage > clientReportedPacketDamage);
    }

    [Fact]
    public void DeterministicRollValueFollowsLegacyBaseGetDamageShape()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_010,
            monsterDefense: 0,
            monsterDefense2: 10_000,
            attackerLevel: 100,
            monsterLevel: 110,
            deterministicRoll: 8);

        Assert.Equal(1_004U, damage);
    }

    [Fact]
    public void MaximumRollSelectsHighestLegacyRollNotRawMaxAttack()
    {
        var damage = LegacyPlayerMonsterPhysicalDamageCalculator.Calculate(
            minAttack: 1_000,
            maxAttack: 1_010,
            monsterDefense: 0,
            monsterDefense2: 10_000,
            attackerLevel: 100,
            monsterLevel: 110,
            attackRoll: LegacyPlayerMonsterPhysicalAttackRoll.MaximumRoll);

        Assert.Equal(1_004U, damage);
    }
}
