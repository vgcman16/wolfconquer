using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly (int Flag, string Key)[] LegacyNormalDeathRegistrationDeadlines =
    [
        (LegacyCleanSweepFlag, "CleanSweepExpiresAtUtc"), (LegacyCleanSweepFlag, "LegacyCleanSweepExpiresAtUtc"),
        (LegacyAxeShadowFlag, "AxeShadowExpiresAtUtc"), (LegacyAxeShadowFlag, "LegacyAxeShadowExpiresAtUtc"),
        (LegacyCelestialDanceFlag, "CelestialDanceExpiresAtUtc"), (LegacyCelestialDanceFlag, "LegacyCelestialDanceExpiresAtUtc"),
        (LegacyHookMoonFlag, "HookMoonExpiresAtUtc"), (LegacyHookMoonFlag, "HookMoon"),
        (LegacyHookMoonCasterFlag, "HookMoonCasterExpiresAtUtc"), (LegacyHookMoonCasterFlag, "HookMoonCaster"),
        (LegacyFootballBallFlag, "LegacyFootballBallExpiresAtUtc"),
        (LegacyBonePulseStatusFlag, "BonePulseExpiresAtUtc"),
        (LegacyScarOfEarthFlag, "LegacyScarOfEarthExpiresAtUtc"), (LegacyScarOfEarthFlag, "LegacyScarOfEarthNextInvokeAtUtc"),
        (LegacyRuntimeUpdateTicker.XpListFlag, RuntimeLegacyKillingFlashXpListExpiresAtUtcAttribute),
        (LegacyRuntimeUpdateTicker.XpListFlag, "LegacyMirrorOfSinXpListExpiresAtUtc"),
    ];

    private static CharacterSnapshot RetireLegacyNormalDeathRegistrationDeadlines(
        CharacterSnapshot character, HashSet<int> removed)
    {
        foreach (var (flag, key) in LegacyNormalDeathRegistrationDeadlines)
            if (removed.Contains(flag)) character = WithoutCharacterAttribute(character, key);
        // All actual OpenXpSkill flags are true registrations. These authoritative
        // arrays include their owned periodic-attack schedules, not XPCount/power.
        if (LegacyXpSkillFlags.Any(removed.Contains))
            foreach (var key in LegacyXpSkillExpiresAtAttributes)
                character = WithoutCharacterAttribute(character, key);
        // Do NOT clear Aura identity/level/damage/GDamage: GetClear does not call
        // DecreaseStatusAura. Source CheckAura later reapplies stored UseAura
        // without a live flag; the local stat consumer still conflates them.
        // PoisonLevel, gear powers/charges, cooldown stamps and Scar owner/spell
        // snapshots likewise remain independent fields, not blanket death resets.
        return character;
    }

    private static WorldSession ClearLegacyNormalDeathExplicitFlag(WorldSession session, int flag)
    {
        var character = LegacyStatusFlagDeathRegistration.Forget(
            WithoutLegacyStatusFlag(session.Character, flag), flag);
        if (flag == LegacyFreezeFlag)
            character = WithoutCharacterAttribute(character, RuntimeLegacyFreezeWarExpiresAtUtcAttribute);
        if ((flag is LegacyTyrantAuraStatusFlag or LegacyFendAuraStatusFlag) &&
            ReadAttributeUInt64(character.Attributes, RuntimeLegacyAuraStatusFlagAttribute) == (ulong)flag)
        {
            character = WithoutCharacterAttribute(character, RuntimeLegacyAuraExpiresAtUtcAttribute);
            character = WithoutCharacterAttribute(character, RuntimeLegacyAuraRemoveOnDeadAttribute);
        }
        if (flag == LegacyFatalStrikeFlag)
            character = WithoutCharacterAttribute(character, RuntimeLegacyFatalStrikeExpiresAtUtcAttribute);
        return session with { Character = character };
    }
}
