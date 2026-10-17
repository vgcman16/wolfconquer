namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const ushort MatchingPounceSpellId = 12_770;
    private const ushort MatchingThunderRampageSpellId = 15_780;
    private const ushort MatchingSerenitySpellId = 10_400;
    private const ushort MatchingTranquilitySpellId = 10_425;
    private const ushort MatchingCompassionSpellId = 10_430;
    private const string PounceRuntimeSeam = "pounce-combat-runtime";
    private const string ThunderRampageRuntimeSeam = "thunderrampage-combat-runtime";
    private const string RemoveBuffersRuntimeSeam = "remove-buffers-runtime";

    private static bool TryClassifyQuaternaryClassSkillRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (IsSupportedPounceRuntimeSpell(sort, spellId))
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile Pounce 12770 after source resource preparation: " +
                    "the active-left shield gate, silent direct shift to the cast point, moved-caster radius-five " +
                    "monster/player/SOB physical damage without extra player scaling, source progress, and the " +
                    "unconditional cast-point animation. ShurikenVortex/ManiacDance return fixed one before WildPhoenix; " +
                    "those returns skip BackDmg and physical MudWall. The normal player path applies DefensiveStance's " +
                    "half damage and Block, continues through bounded Heaven's Wonder/RevengeTail BackDmg, then applies " +
                    "MudWall once before soul and the opening CombatGear, speffect 2/3/4/5, and Ninja Chance / 100 receive hooks. " +
                    "Each candidate rechecks current visibility, instance, inclusive radius, and attack eligibility once before " +
                    "its flashing-name, counter, or passive effects; receive modifiers, HP, and publication use the refreshed state " +
                    "before the next player. " +
                    "Other Physical/ReceiveAttack.Player branches and visible exact-D13 proof remain partial.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/Pounce.cs handles sort 84 spell 12770, calls Shift with " +
                    "SendData false before selecting radius-five targets, and sends MsgSpellAnimation after progress. " +
                    "Calculate/Physical.cs returns for player status and WildPhoenix, halves DefensiveStance without " +
                    "returning, then calls BackDmg before MudWall. ReceiveAttack/Player.cs opens with CombatGear.Update/UpdateTarget, " +
                    "speffect groups 2/3/4/5, and Ninja gouyu_immortals Chance / 100 before damage modifiers and HP. " +
                    "RoleView.Roles/CanSee gates the current map and DynamicID before Pounce's radius and CanAttackPlayer check. " +
                    ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: capture one visible exact-D13 Pounce shift and mixed-target cast.",
                nextSeam: PounceRuntimeSeam);
            return true;
        }

        if (IsSupportedThunderRampageRuntimeSpell(sort, spellId))
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.XpSkill,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile ThunderRampage 15780 after source resource preparation: " +
                    "the ThunderStriker class gate, XPList stamina refill and 40-second status transition, active " +
                    "cast-point monster/SOB radius-eleven and player radius-nine physical damage, source player " +
                    "double damage, uncapped mixed targets, progress before animation, and empty inactive animation. " +
                    "Remaining proof is one visible exact-D13 activation and active cast.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/ThunderRampage.cs handles sort 94 spell 15780, uses " +
                    "a 500ms source attack stamp, and always sends MsgSpellAnimation after its XPList or active branch. " +
                    ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill XP owner: capture one visible exact-D13 ThunderRampage activation and active mixed-target cast.",
                nextSeam: ThunderRampageRuntimeSeam);
            return true;
        }

        if (IsSupportedRemoveBuffersRuntimeSpell(sort, spellId))
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the matching-decompile RemoveBuffers family after source resource " +
                    "preparation: Serenity 10400, Tranquility 10425 with its learned GraceOfHeaven 12560 team " +
                    "follow-up, and Compassion 10430. The runtime removes the exact source status sets, clears " +
                    "their local timed-status metadata, includes the caster in strict distance-under-eighteen " +
                    "team enumeration without a same-map gate, publishes D13 status vectors, and preserves " +
                    "animation-before-progress ordering. Remaining proof is one visible exact-D13 cast per family.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/RemoveBuffers.cs handles sort 46 spell 10400, sort 47 " +
                    "spell 10425, and sort 50 spell 10430. Matching Team.cs inserts the owner into Members and " +
                    "Temates/GetMembers enumerate that collection; matching magictype.txt supplies the exact " +
                    "resource, 500ms attack-stamp, and ColdTime rows. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Monk support owner: capture visible D13 Serenity, Tranquility plus GraceOfHeaven, and Compassion casts.",
                nextSeam: RemoveBuffersRuntimeSeam);
            return true;
        }

        info = default;
        return false;
    }

    public static bool IsSupportedPounceRuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Pounce && spellId == MatchingPounceSpellId;
    }

    public static bool IsSupportedThunderRampageRuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.ThunderRampage && spellId == MatchingThunderRampageSpellId;
    }

    public static bool IsSupportedRemoveBuffersRuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return (sort == LegacyMagicSort.RemoveBuffers && spellId == MatchingSerenitySpellId) ||
            (sort == LegacyMagicSort.Tranquility && spellId == MatchingTranquilitySpellId) ||
            (sort == LegacyMagicSort.Compasion && spellId == MatchingCompassionSpellId);
    }
}
