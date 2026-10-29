namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Pure current-record eligibility. The existing admission owner/CAS is the
    // transaction boundary; this does not authorize a second writer or queue.
    private static bool TryValidateLegacyXpAdmissionPrerequisites(
        WorldSession expected, LegacyMagicRuntimeSpell spell, out string reason)
    {
        reason = string.Empty;
        bool restoreStamina;
        if (spell.Sort == LegacyMagicSort.AttachStatus && TryGetLegacyAttachStatusOpenXpSkill(
                spell.Id, out _, out _, out _, out var name, out restoreStamina))
        {
            if (!HasLegacyStatusFlag(expected.Character, LegacyRuntimeUpdateTicker.XpListFlag))
            {
                reason = $"{name}: XPList status flag is not active";
                return false;
            }
        }
        else if (TryGetLegacySpecialXpSkillDefinition(spell, out var definition))
        {
            if (definition.RequiresThunderstrikerClass && !IsLegacyThunderstrikerClass(expected.Character.Class))
            {
                reason = $"{definition.Name}: required Thunderstriker class is absent";
                return false;
            }
            // No-XP special damage is a different valid branch, not an XP
            // activation that may be disabled by a blanket entitlement check.
            restoreStamina = definition.RestoreStaminaToMaximum &&
                HasLegacyStatusFlag(expected.Character, LegacyRuntimeUpdateTicker.XpListFlag);
        }
        else return true;

        if (restoreStamina && !TryReadLegacyRuntimeStamina(expected.Character, out _))
        {
            reason = "xpRestorationStaminaInvalid";
            return false;
        }
        return true;
    }
}
