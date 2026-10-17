using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPhysicalBreakthroughRefineryRollAttribute =
        "LegacyPhysicalBreakthroughRefineryRoll";

    private readonly record struct LegacyPlayerPlayerPhysicalBreakthroughState(
        bool IsModeled,
        bool Penetrated);

    private readonly record struct LegacyPlayerPlayerPhysicalBreakthroughResult(
        LegacyPlayerPlayerPhysicalSpellDamageResult DamageResult,
        LegacyPlayerPlayerPhysicalBreakthroughState State);

    private static LegacyPlayerPlayerPhysicalBreakthroughResult
        ApplyLegacyPlayerPlayerPhysicalSpellBreakthroughBranch(
            WorldSession attacker,
            WorldSession target,
            LegacyMagicRuntimeSpell spell,
            WorldDataCatalog? worldData,
            LegacyPlayerPlayerPhysicalSpellDamageResult damageResult,
            bool applyPenetrationCompensation = true)
    {
        if (worldData is null)
        {
            return new(
                damageResult,
                new LegacyPlayerPlayerPhysicalBreakthroughState(IsModeled: false, Penetrated: false));
        }

        var state = new LegacyPlayerPlayerPhysicalBreakthroughState(IsModeled: true, Penetrated: false);
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var priorPhysicalEffectMask =
            MsgAttackEffect.CriticalStrike |
            MsgAttackEffect.LuckyStrike |
            MsgAttackEffect.Dash;
        if ((damageResult.Effect & priorPhysicalEffectMask) != 0)
        {
            return new(damageResult, state);
        }

        var attackerBreakthrough = attackerFields[LegacyPlayerStatusPacket.BreakthroughIndex];
        var targetCounteraction = targetFields[LegacyPlayerStatusPacket.CounteractionIndex];
        if (!PassesLegacyPlayerPlayerPhysicalBreakthroughRefinery(
                attacker.Character,
                attackerBreakthrough,
                targetCounteraction))
        {
            return new(damageResult, state);
        }

        var damage = damageResult.Damage;
        var effect = damageResult.Effect;
        var penetrated = CalculateLegacyPlayerPlayerPhysicalBattlePower(attacker, worldData) <
                CalculateLegacyPlayerPlayerPhysicalBattlePower(target, worldData) &&
            !HasLegacyStatusFlag(attacker.Character, LegacySacrificeFlag) &&
            !HasLegacyStatusFlag(attacker.Character, LegacyDeathSighFlag);
        if (penetrated)
        {
            var penetrationPercent = spell.Id is 16_480 or 16_490 ? 45U : 30U;
            damage = AddLegacySaturating(
                damage,
                MulDivToUInt32(damage, penetrationPercent, 100));
            effect |= MsgAttackEffect.Penetration;
        }

        if (TryGetLegacyEligibleEquippedRuneLevel(target, worldData, "Barrier", out var barrierLevel))
        {
            var barrierPercent = ResolveLegacyPhysicalBarrierPercent(barrierLevel);
            damage -= MulDivToUInt32(damage, barrierPercent, 100);

            if (TryGetLegacyEligibleEquippedRuneLevel(attacker, worldData, "TacitStrike", out var tacitStrikeLevel))
            {
                var tacitStrikePercent = ResolveLegacyPhysicalTacitStrikePercent(tacitStrikeLevel);
                damage = AddLegacySaturating(
                    damage,
                    MulDivToUInt32(damage, tacitStrikePercent, 100));
            }
        }

        if (penetrated && applyPenetrationCompensation)
        {
            damage = ApplyLegacyPlayerPlayerPhysicalPenetrationCompensation(
                attacker,
                target,
                spell,
                damage);
        }

        return new(
            damageResult with
            {
                Damage = damage,
                Effect = effect,
            },
            state with { Penetrated = penetrated });
    }

    private static LegacyPlayerPlayerPhysicalSpellDamageResult
        ApplyLegacyPlayerPlayerPhysicalSpellNoBreakthroughBonus(
            LegacyPlayerPlayerPhysicalBreakthroughState breakthroughState,
            LegacyPlayerPlayerPhysicalSpellDamageResult damageResult)
    {
        if (!breakthroughState.IsModeled || breakthroughState.Penetrated)
        {
            return damageResult;
        }

        return damageResult with
        {
            Damage = AddLegacySaturating(
                damageResult.Damage,
                MulDivToUInt32(damageResult.Damage, 50, 100)),
        };
    }

    private static bool PassesLegacyPlayerPlayerPhysicalBreakthroughRefinery(
        CharacterSnapshot attacker,
        uint attackerBreakthrough,
        uint targetCounteraction)
    {
        if (attackerBreakthrough <= targetCounteraction)
        {
            return false;
        }

        var delta = (ulong)attackerBreakthrough - targetCounteraction;
        var roll = TryReadUInt32Attribute(
            attacker.Attributes,
            out var forcedRoll,
            LegacyPhysicalBreakthroughRefineryRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < delta;
    }

    private static ulong CalculateLegacyPlayerPlayerPhysicalBattlePower(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        var character = session.Character;
        var explicitBattlePower = ReadAttributeUInt64(
            character.Attributes,
            "BattlePower",
            "BattlePowerValue");
        if (explicitBattlePower != 0)
        {
            return explicitBattlePower;
        }

        var nobilityRank = ResolveLegacyPlayerPlayerPhysicalNobilityRank(character);
        var battlePower = 0UL;
        battlePower = AddLegacyPlayerPlayerPhysicalBattlePowerComponent(
            battlePower,
            ResolveLegacyPlayerPlayerPhysicalProfessionBattlePower(character, worldData));
        battlePower = AddLegacyPlayerPlayerPhysicalBattlePowerComponent(
            battlePower,
            character.Level > 0 ? (ulong)character.Level : 0);
        battlePower = AddLegacyPlayerPlayerPhysicalBattlePowerComponent(
            battlePower,
            ResolveLegacyPlayerPlayerPhysicalRebornBattlePower(character.Reborn));
        battlePower = AddLegacyPlayerPlayerPhysicalBattlePowerComponent(
            battlePower,
            LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(
                session.PlayerItems,
                session.AlternateEquipmentActive));
        battlePower = AddLegacyPlayerPlayerPhysicalBattlePowerComponent(battlePower, nobilityRank);
        battlePower = AddLegacyPlayerPlayerPhysicalBattlePowerComponent(
            battlePower,
            ReadAttributeUInt64(character.Attributes, "ExtraBattlePower"));

        var maxBattlePower = nobilityRank switch
        {
            LegacyNobilityRanking.RankKing => 522UL,
            LegacyNobilityRanking.RankPrince => 519UL,
            LegacyNobilityRanking.RankDuke => 517UL,
            _ => 516UL,
        };
        return Math.Min(battlePower, maxBattlePower);
    }

    private static ulong ResolveLegacyPlayerPlayerPhysicalProfessionBattlePower(
        CharacterSnapshot character,
        WorldDataCatalog worldData)
    {
        if (character.Class < 0 ||
            character.Class > int.MaxValue ||
            !worldData.ProfessionLevelBenefitsByProfessionCode.TryGetValue(
                (int)character.Class,
                out var benefit) ||
            benefit.Rank <= 0)
        {
            return 0;
        }

        return checked((ulong)benefit.Rank);
    }

    private static ulong ResolveLegacyPlayerPlayerPhysicalRebornBattlePower(long reborn)
    {
        if (reborn <= 0)
        {
            return 0;
        }

        return reborn >= 105 ? 522UL : checked((ulong)reborn * 5UL);
    }

    private static ulong ResolveLegacyPlayerPlayerPhysicalNobilityRank(CharacterSnapshot character)
    {
        if (character.PaidNobility.TryGetValue("PaidRank", out var numericRank) &&
            ulong.TryParse(numericRank, out var paidRank))
        {
            return paidRank;
        }

        if (character.PaidNobility.TryGetValue("Rank", out var rankName))
        {
            return rankName.Trim() switch
            {
                "Knight" => 1,
                "Baron" => 3,
                "Earl" => LegacyNobilityRanking.RankEarl,
                "Duke" => LegacyNobilityRanking.RankDuke,
                "Prince" => LegacyNobilityRanking.RankPrince,
                "King" => LegacyNobilityRanking.RankKing,
                _ => 0,
            };
        }

        return ReadAttributeUInt64(character.Attributes, "NobilityRank");
    }

    private static ulong AddLegacyPlayerPlayerPhysicalBattlePowerComponent(ulong total, ulong component)
    {
        return Math.Min(522UL, total + Math.Min(component, 522UL));
    }

    private static bool TryGetLegacyEligibleEquippedRuneLevel(
        WorldSession session,
        WorldDataCatalog worldData,
        string runeName,
        out byte level)
    {
        level = 0;
        return session.Character.Reborn >= 2 &&
            session.Character.Level >= 15 &&
            LegacyMagicResourceCostCalculator.TryGetEquippedRuneLevel(
                session.PlayerItems,
                worldData.ItemsById,
                runeName,
                session.AlternateEquipmentActive,
                session.Character.Attributes,
                session.Character.Class,
                out level);
    }

    private static uint ResolveLegacyPhysicalBarrierPercent(byte level)
    {
        return level switch
        {
            1 or 2 => 5,
            3 or 4 => 10,
            5 or 6 => 15,
            7 or 8 or 9 => 20,
            _ => 0,
        };
    }

    private static uint ResolveLegacyPhysicalTacitStrikePercent(byte level)
    {
        return level switch
        {
            1 or 2 => 5,
            3 or 4 => 10,
            5 or 6 => 15,
            7 => 20,
            8 => 25,
            9 => 30,
            _ => 0,
        };
    }

    private static uint ApplyLegacyPlayerPlayerPhysicalPenetrationCompensation(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell spell,
        uint damage)
    {
        var divisor = spell.Id == 12_080 &&
            LegacyGetDistance(
                attacker.Character.X,
                attacker.Character.Y,
                target.Character.X,
                target.Character.Y) <= 3
            ? 0.66
            : 0.67;
        var compensatedDamage = damage / divisor;
        return compensatedDamage >= uint.MaxValue
            ? uint.MaxValue
            : (uint)compensatedDamage;
    }
}
