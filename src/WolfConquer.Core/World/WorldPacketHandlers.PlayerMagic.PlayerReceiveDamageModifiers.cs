using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyPlayerReceiveDamageModifierResult(
        uint Damage,
        CharacterSnapshot Attacker,
        CharacterSnapshot Target,
        bool HawksAmbitionConsumed,
        uint? LightningShieldActivationLeft,
        uint? LightningShieldDrainLeft,
        uint LightningShieldSpellLevel,
        bool DuelCleanupApplied,
        bool AttackerAbsolutionConsumed,
        bool TargetAbsolutionConsumed,
        bool BloodTideActivated,
        uint BloodTideHitpoints,
        uint BloodTideDuration,
        byte BloodTideSpellLevel,
        bool ReverseMagicApplied,
        bool CapriceStaminaTransferred,
        bool CapriceXpListRemoved,
        bool XpBoosterApplied,
        bool XpBoosterDepleted,
        bool XpKillerApplied,
        bool TidalWaveApplied,
        byte TidalWaveValue,
        bool QuenchApplied,
        byte QuenchValue,
        bool AttackHitApplied,
        bool TargetNeptuneCurseBlocked,
        bool AttackerNeptuneCurseCapped);

    private static LegacyPlayerReceiveDamageModifierResult ApplyLegacyPlayerReceiveDamageModifiers(
        CharacterSnapshot attacker,
        WorldSession? attackerSession,
        WorldSession targetSession,
        LegacyPlayerReceiveEarlyModifierResult early,
        WorldDataCatalog? worldData)
    {
        var adjustedDamage = early.Damage;
        var updatedTarget = targetSession.Character;
        var updatedAttacker = attacker;
        var hawksAmbitionConsumed = early.HawksAmbitionConsumed;
        var lightningShieldActivationLeft = early.LightningShieldActivationLeft;
        var lightningShieldDrainLeft = early.LightningShieldDrainLeft;
        var lightningShieldSpellLevel = early.LightningShieldSpellLevel;

        var duelCleanupApplied = false;
        if (HasLegacyStatusFlag(updatedTarget, LegacyDuelFlag))
        {
            updatedTarget = WithoutLegacyStatusFlag(updatedTarget, LegacyDuelFlag);
            updatedTarget = WithCharacterAttribute(updatedTarget, "XPCount", 0);
            updatedTarget = WithCharacterAttribute(
                updatedTarget,
                "LegacyDuelEndStampUtc",
                DateTimeOffset.UtcNow.ToString("O"));
            updatedTarget = WithCharacterAttribute(updatedTarget, "LegacyDuelXpDurationAdjustmentSeconds", "-20");
            duelCleanupApplied = true;
        }

        var attackerAbsolutionConsumed = false;
        if (HasLegacyStatusFlag(updatedAttacker, LegacyAbsolutionFlag))
        {
            updatedAttacker = WithoutLegacyStatusFlag(updatedAttacker, LegacyAbsolutionFlag);
            attackerAbsolutionConsumed = true;
        }

        var targetAbsolutionConsumed = false;
        if (HasLegacyStatusFlag(updatedTarget, LegacyAbsolutionFlag))
        {
            updatedTarget = WithoutLegacyStatusFlag(updatedTarget, LegacyAbsolutionFlag);
            targetAbsolutionConsumed = true;
        }

        var bloodTideActivated = TryApplyLegacyPlayerReceiveBloodTide(
            updatedAttacker,
            attackerSession,
            targetSession,
            worldData,
            out updatedAttacker,
            out var bloodTideHitpoints,
            out var bloodTideDuration,
            out var bloodTideSpellLevel);

        var capriceResult = TryApplyLegacyPlayerReceiveCaprice(
            updatedAttacker,
            updatedTarget,
            attackerSession,
            targetSession,
            out updatedAttacker,
            out updatedTarget);

        var reverseMagicApplied = TryApplyLegacyPlayerReceiveReverseMagic(
            updatedAttacker,
            attackerSession,
            targetSession,
            out updatedAttacker);

        TryApplyLegacyPlayerReceiveSlowPace(
            updatedAttacker,
            updatedTarget,
            attackerSession,
            targetSession,
            worldData,
            out updatedTarget);

        var xpRunesResult = TryApplyLegacyPlayerReceiveXpRunes(
            updatedAttacker,
            updatedTarget,
            attackerSession,
            out updatedAttacker,
            out updatedTarget);

        var neptuneCurseResult = ApplyLegacyPlayerReceiveNeptuneCurse(
            updatedAttacker,
            updatedTarget,
            adjustedDamage);
        adjustedDamage = neptuneCurseResult.Damage;
        updatedAttacker = neptuneCurseResult.Attacker;

        return new LegacyPlayerReceiveDamageModifierResult(
            adjustedDamage,
            updatedAttacker,
            updatedTarget,
            hawksAmbitionConsumed,
            lightningShieldActivationLeft,
            lightningShieldDrainLeft,
            lightningShieldSpellLevel,
            duelCleanupApplied,
            attackerAbsolutionConsumed,
            targetAbsolutionConsumed,
            bloodTideActivated,
            bloodTideHitpoints,
            bloodTideDuration,
            bloodTideSpellLevel,
            reverseMagicApplied,
            capriceResult.StaminaTransferred,
            capriceResult.XpListRemoved,
            xpRunesResult.XpBoosterApplied,
            xpRunesResult.XpBoosterDepleted,
            xpRunesResult.XpKillerApplied,
            xpRunesResult.TidalWaveApplied,
            xpRunesResult.TidalWaveValue,
            xpRunesResult.QuenchApplied,
            xpRunesResult.QuenchValue,
            neptuneCurseResult.AttackHitApplied,
            neptuneCurseResult.TargetNeptuneCurseBlocked,
            neptuneCurseResult.AttackerNeptuneCurseCapped);
    }
}
