using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyPhysicalPlayerLuckyBranchRollAttribute = "LegacyPhysicalPlayerLuckyBranchRoll";
    private const string RuntimeLegacyPhysicalPlayerCriticalRollAttribute = "LegacyPhysicalPlayerCriticalRoll";
    private const string RuntimeLegacyPhysicalPlayerStrikeLockRollAttribute = "LegacyPhysicalPlayerStrikeLockRoll";

    private readonly record struct LegacyPlayerPlayerPhysicalEffectsResolution(
        LegacyPlayerPlayerPhysicalSpellDamageResult DamageResult,
        LegacyRefineEffect RefineEffect = LegacyRefineEffect.None);

    private static LegacyPlayerPlayerPhysicalEffectsResolution ResolveLegacyPlayerPlayerPhysicalEffects(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell? spell,
        WorldDataCatalog worldData,
        LegacyPlayerPlayerPhysicalSpellDamageResult damageResult)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var criticalStrike = attackerFields[LegacyPlayerStatusPacket.CriticalStrikeIndex];
        if (criticalStrike == 0)
        {
            return new(damageResult);
        }

        return ResolveLegacyPlayerPlayerPhysicalEffectsCore(attacker, target, spell, worldData, damageResult);
    }

    private static LegacyPlayerPlayerPhysicalEffectsResolution ResolveLegacyPlayerPlayerPhysicalEffectsCore(
        WorldSession attacker,
        WorldSession target,
        LegacyMagicRuntimeSpell? spell,
        WorldDataCatalog worldData,
        LegacyPlayerPlayerPhysicalSpellDamageResult damageResult)
    {
        // Outer entry was already accepted before the mutable313 publication.
        // Source489/490/535 read current inner stats without re-entering400.
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var criticalStrike = attackerFields[LegacyPlayerStatusPacket.CriticalStrikeIndex];
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        var criticalPercent = criticalStrike / 100;
        var immunityPercent = targetFields[LegacyPlayerStatusPacket.ImmunityIndex] / 100;
        var refineryPassed = criticalPercent > immunityPercent &&
            CheckLegacyPlayerPlayerPhysicalEffectRate(
                criticalPercent - immunityPercent,
                ReadLegacyPhysicalEffectRoll(attacker.Character, RuntimeLegacyPhysicalRefineryRollAttribute));
        if (refineryPassed &&
            CalculateLegacyPlayerPlayerPhysicalBattlePower(attacker, worldData) >=
                CalculateLegacyPlayerPlayerPhysicalBattlePower(target, worldData))
        {
            if (CheckLegacyPlayerPlayerPhysicalEffectRate(
                    1, ReadLegacyPhysicalEffectRoll(attacker.Character, RuntimeLegacyPhysicalPlayerLuckyBranchRollAttribute)))
            {
                var luckyChance = ReadLegacyPlayerPlayerPhysicalPerfectionChance(
                    attacker.Character, "LuckyStrike");
                if (!CheckLegacyPlayerPlayerPhysicalEffectRate(
                        luckyChance, ReadLegacyPhysicalEffectRoll(attacker.Character, RuntimeLegacyPhysicalLuckyStrikeRollAttribute)))
                {
                    return new(damageResult);
                }

                var strikeLockChance = ReadLegacyPlayerPlayerPhysicalPerfectionChance(
                    target.Character, "StrikeLock");
                if (CheckLegacyPlayerPlayerPhysicalEffectRate(
                        strikeLockChance, ReadLegacyPhysicalEffectRoll(target.Character, RuntimeLegacyPhysicalPlayerStrikeLockRollAttribute)))
                {
                    return new(damageResult, LegacyRefineEffect.StrikeLockLevel);
                }

                return new(
                    damageResult with
                    {
                        Damage = MulDivToUInt32(damageResult.Damage, 200, 100),
                        Effect = damageResult.Effect | MsgAttackEffect.LuckyStrike,
                    },
                    LegacyRefineEffect.LuckyStrike);
            }

            if (CheckLegacyPlayerPlayerPhysicalEffectRate(
                    criticalPercent - immunityPercent,
                    ReadLegacyPhysicalEffectRoll(attacker.Character, RuntimeLegacyPhysicalPlayerCriticalRollAttribute)))
            {
                var strikeBooster = TryGetLegacyEligibleEquippedRuneLevel(attacker, worldData, "StrikeBooster", out _)
                    ? 30U : 0U;
                var whirlSigil = spell is null ? 0U : GetLegacyWhirlSigilSageDamagePercent(attacker, worldData, spell.Id);
                return new(damageResult with
                {
                    Damage = MulDivToUInt32(damageResult.Damage, 170 + whirlSigil + strikeBooster, 100),
                    Effect = damageResult.Effect | MsgAttackEffect.CriticalStrike,
                });
            }

            return new(damageResult);
        }

        var dashRate = TryReadUInt32Attribute(attacker.Character.Attributes, out var storedDashRate, "DashRate")
            ? storedDashRate
            : attackerFields[LegacyPlayerStatusPacket.DashRateIndex];
        if (CheckLegacyPlayerPlayerPhysicalEffectRate(
                dashRate / 100, ReadLegacyPhysicalEffectRoll(attacker.Character, RuntimeLegacyPhysicalDashRollAttribute)))
        {
            var whirlSigil = spell is null ? 0U : GetLegacyWhirlSigilSageDamagePercent(attacker, worldData, spell.Id);
            return new(damageResult with
            {
                Damage = MulDivToUInt32(damageResult.Damage, 150 + whirlSigil, 100),
                Effect = damageResult.Effect | MsgAttackEffect.Dash,
            });
        }

        return new(damageResult);
    }

    private static double ReadLegacyPlayerPlayerPhysicalPerfectionChance(
        CharacterSnapshot character,
        string name)
    {
        return TryReadDoubleAttribute(
                character.Attributes, out var chance,
                $"PerfectionStatus.{name}", $"LegacyPerfectionStatus.{name}")
            ? chance
            : 0.0;
    }

    private static bool CheckLegacyPlayerPlayerPhysicalEffectRate(double chance, uint roll)
    {
        return chance > roll % 100;
    }
}
