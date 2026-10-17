namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Complete concrete-producer matrix: normal-death-known-registration-inventory.md.
    // These are ONE resolved flag key each; enum OR components are NOT separate flags.
    // Freeze59 and DragonSwing160 are deliberately absent: their real source
    // producers disagree and require their exact captured insertion policy.
    private static readonly HashSet<int> LegacyNormalDeathKnownTrueFlags =
    [
        LegacyStarOfAccuracyFlag, LegacyMagicShieldFlag, LegacyStigmaFlag, LegacyInvisibilityFlag,
        LegacyFlyFlag, LegacyCastPrayFlag, LegacyRideFlag, LegacyMagicDefenderFlag, LegacyAzureShieldFlag,
        LegacyPoisonStarFlag, LegacySoulShackleStatusFlag, LegacyRevengeTailFlag, LegacyFrostArrowsStatusFlag,
        LegacyFineRain1Flag, LegacyFineRain2Flag, LegacyInfinityFlag, LegacyIronShieldFlag,
        LegacyImmortalForceFlag, LegacyIronGuardFlag, LegacyRiseOfTaoismFlag, LegacyCrackStarFlag,
        LegacyHawksAmbitionFlag, LegacyBonePulseStatusFlag, LegacyMudWallFlag, LegacyInfiniteMistFlag,
        LegacyRuntimeUpdateTicker.XpListFlag, LegacyPhysicalStatusOverlay.FrontBreakStatusFlag,
        LegacyPlayerDeathState.DeadStatusFlag, LegacyPlayerDeathState.GhostStatusFlag,
        LegacyFocusedFlag, LegacyShieldBlockFlag, LegacyHeavensWrathFlag, LegacyKineticSparkFlag,
        LegacyCleanSweepFlag, LegacyAxeShadowFlag, LegacyCelestialDanceFlag, LegacyFlashingNameStatusFlag,
        LegacyLightningShieldActivatedFlag, LegacyBloodTideFlag, LegacyDivineGuardFlag,
        LegacyTidalWaveFlag, LegacyQuenchFlag, LegacyVenomFlag, LegacyAttackStatus10800ShieldOfTruthFlag,
        LegacyDuelFlag, LegacyHookMoonFlag, LegacyHookMoonCasterFlag, LegacyXChillingSnowFlag,
        LegacyScurvyBombFlag, LegacyScarOfEarthFlag, LegacySolidFlag, LegacyCombatGearSageSpeedModeFlag,
        0x76, LegacyFootballBallFlag, 1, // CTF; Football; actual Toxic6001 Poisoned.
        LegacyTyrantAuraStatusFlag, LegacyFendAuraStatusFlag, 0x66, 0x68, 0x6A, 0x6C, 0x6E,

        // Fixed CombatGear registrations. DB-selected paths additionally capture
        // their resolved integer so new dynamic flags are not guessed by name.
        LegacyCombatGearLightningMagicShieldTop4WeeklyFlag, LegacyAzureShieldFlag | LegacyPaperDanceFlag,
        LegacyTopWaterTaoistFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag,
        LegacyFireAuraFlag | LegacyHawksAmbitionFlag, LegacyBlackbeardsRageFlag | LegacyHawksAmbitionFlag,
        LegacyConfusedFlag | LegacyTop4WeeklyFlag | LegacyHawksAmbitionFlag,
        LegacyVenomFlag | LegacyMagicShieldFlag, LegacyCombatGearColdBloodlineFlag,
        LegacyCombatGearPhoenixBlessingFlag, LegacyCombatGearHolyProtectionFlag, LegacyCombatGearWildPhoenixFlag,
        LegacyCombatGearPrisonSigilFutilityCursedFlag, LegacyCombatGearWhirlSigilAuraCursedFlag,
        LegacyCombatGearTop8WeeklyHawksAmbitionFlag, LegacyCombatGearTop4WeeklyHawksAmbitionFlag,
        LegacyCombatGearTop2WeeklyHawksAmbitionFlag, LegacyCombatGearArchShadowFlag,
        LegacyCombatGearWaterTop4WeeklyFlag, LegacyCombatGearLightningTop4WeeklyFlag,
        LegacyCombatGearSageSpeedModeTop4WeeklyFlag,
        0x12F, 0x130, 0x133, 0x134, 0x135, 0x136, // Immersion/Insouciance; DragonPierce modes.
        0x7A | LegacyHawksAmbitionFlag, 0x7B | LegacyHawksAmbitionFlag, 0x7E | LegacyHawksAmbitionFlag,
        0x19D, 0x19C, // Pirate Armed/Sense; independent powers remain.
    ];
}
