namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Source registrations, not independent powers. Known true defaults are
    // audited separately; explicit false/captured registration provenance wins.
    private static readonly (int Flag, string Prefix)[] LegacyNormalDeathRegistrations =
    [
        (LegacyStarOfAccuracyFlag, "LegacyAccuracy"), (LegacyMagicShieldFlag, "LegacyMagicShield"),
        (LegacyStigmaFlag, "LegacyStigma"), (LegacyInvisibilityFlag, "LegacyInvisibility"),
        (LegacyFlyFlag, "LegacyFly"), (LegacyCastPrayFlag, "LegacyCastPray"), (LegacyRideFlag, "LegacyRide"),
        (LegacyMagicDefenderFlag, "LegacyAuraMagicDefender"), (LegacyAzureShieldFlag, "LegacyMagicDefender"),
        (LegacyPoisonStarFlag, "LegacyPoisonStar"), (LegacySoulShackleStatusFlag, "LegacySoulShackle"),
        (LegacyRevengeTailFlag, "LegacyRevengeTail"), (LegacyFrostArrowsStatusFlag, "LegacyFrostArrows"),
        (LegacyFineRain1Flag, "LegacyFineRain1"), (LegacyFineRain2Flag, "LegacyFineRain2"),
        (LegacyInfinityFlag, "LegacyInfinity"), (LegacyIronShieldFlag, "LegacyIronShield"),
        (LegacyImmortalForceFlag, "LegacyImmortalForce"), (LegacyIronGuardFlag, "LegacyIronGuard"),
        (LegacyRiseOfTaoismFlag, "LegacyRiseOfTaoism"), (LegacyCrackStarFlag, "LegacyCrackStar"),
        (LegacyHawksAmbitionFlag, "LegacyHawksAmbition"), (LegacyBonePulseStatusFlag, "LegacyBonePulse"),
        (LegacyMudWallFlag, "LegacyMudWall"), (LegacyInfiniteMistFlag, "LegacyInfiniteMist"),
        (LegacyFocusedFlag, "LegacyFocused"), (LegacyShieldBlockFlag, "LegacyShieldBlock"),
        (LegacyHeavensWrathFlag, "LegacyHeavensWrath"), (LegacyKineticSparkFlag, "LegacyKineticSpark"),
        (LegacyBloodTideFlag, "LegacyBloodTide"), (LegacyDivineGuardFlag, "LegacyDivineGuard"),
        (LegacyTidalWaveFlag, "LegacyTidalWave"), (LegacyQuenchFlag, "LegacyQuench"),
        (LegacyVenomFlag, "LegacyVenom"), (LegacyAttackStatus10800ShieldOfTruthFlag, "LegacyShieldofTruth"),
        (LegacyDuelFlag, "LegacyDuel"), (1, "LegacyPoisoned"),
        (LegacyTyrantAuraStatusFlag, "LegacyAura"), (LegacyFendAuraStatusFlag, "LegacyAura"),
        (0x66, "LegacyAura"), (0x68, "LegacyAura"), (0x6A, "LegacyAura"),
        (0x6C, "LegacyAura"), (0x6E, "LegacyAura"),
        (LegacyDragonSwingFlag, "LegacyDragonSwing"), (LegacyFreezeFlag, "LegacyFreezeWar"),
        (LegacyBossStatusEffect.FrightenedFlag, "LegacyFrightened"),
        (LegacyBossStatusEffect.DizzyFlag, "LegacyDustSigilStunn"),
    ];

    private static WorldSession ClearLegacyNormalDeathRegistrations(WorldSession session)
    {
        var character = session.Character;
        var removed = new HashSet<int>();
        var flags = LegacyNormalDeathRegistrations.Select(entry => entry.Flag)
            .Concat(LegacyNormalDeathKnownTrueFlags).Concat(LegacyXpSkillFlags)
            .Concat(LegacyStatusFlagDeathRegistration.GetDeathRemovableFlags(character)).Distinct();
        foreach (var flag in flags)
        {
            if (!HasLegacyStatusFlag(character, flag)) continue;
            var remove = LegacyStatusFlagDeathRegistration.TryRead(character, flag, out var captured)
                ? captured : IsLegacyNormalDeathKnownTrueRegistration(character, flag);
            if (!remove) continue;
            removed.Add(flag);
            character = LegacyStatusFlagDeathRegistration.Forget(WithoutLegacyStatusFlag(character, flag), flag);
            foreach (var (_, prefix) in LegacyNormalDeathRegistrations.Where(entry => entry.Flag == flag))
            {
                character = WithoutCharacterAttribute(character, prefix + "RemoveOnDead");
                character = WithoutCharacterAttribute(character, prefix + "ExpiresAtUtc");
            }
        }

        character = RetireLegacyNormalDeathRegistrationDeadlines(character, removed);
        return session with
        {
            Character = character,
            PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(session) with
            {
                FrontBreakExpiresAtUtc = removed.Contains(LegacyPhysicalStatusOverlay.FrontBreakStatusFlag)
                    ? null : LegacyPhysicalStatusOverlay.ForOwner(session).FrontBreakExpiresAtUtc,
            },
        };
    }

    private static bool IsLegacyNormalDeathKnownTrueRegistration(
        WolfConquer.Core.Data.CharacterSnapshot character, int flag)
    {
        foreach (var (_, prefix) in LegacyNormalDeathRegistrations.Where(entry => entry.Flag == flag))
            if (character.Attributes.TryGetValue(prefix + "RemoveOnDead", out var raw))
                return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || raw == "1";
        return LegacyNormalDeathKnownTrueFlags.Contains(flag) || LegacyXpSkillFlags.Contains(flag);
    }
}
