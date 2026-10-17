namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const string StarArrowRuntimeSeam = "stararrow-combat-runtime";
    private const string FatalSpinRuntimeSeam = "fatalspin-combat-runtime";
    private const string RemoveStaminRuntimeSeam = "remove-stamin-state-runtime";

    private static bool TryClassifyTertiaryClassSkillRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (sort == LegacyMagicSort.StarArrow && spellId == 10_313)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile StarArrow 10313 after source resource/cooldown preparation: " +
                    "the selected visible monster or player is pulled to the caster before range and attack checks, " +
                    "monster/player/SOB ranged damage and soul scaling, and the player NatureShield post-pull avoidance roll. " +
                    "The player Range.OnPlayer branch preserves ShurikenVortex/ManiacDance before WildPhoenix, applies zero-base " +
                    "CalculateExtraAttack with attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease for those statuses, " +
                    "fixes only WildPhoenix at one damage, applies DefensiveStance's 40 percent reduction with Block before returning, " +
                    "and invokes the bounded learned-defender Heaven's Wonder 16350 BackDmg caller only on the normal ranged " +
                    "path before the incoming animation. After those Range decisions, the opening ReceiveAttack.Player " +
                    "CombatGear, speffect groups 2/3/4/5, and Ninja Chance / 100 hooks run before receive modifiers and HP. " +
                    "Source MoveX/MoveY metadata, nested attacker state, spell progress, " +
                    "and the final animation even when no target is hit are preserved. Remaining proof is one visible exact-D13 " +
                    "pull, avoidance, and hit sequence.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/StarArrow.cs handles only spell 10313 and calls " +
                    "AttackHandler.Calculate/Range.cs for player damage; Range.cs applies zero-base CalculateExtraAttack for " +
                    "ShurikenVortex/ManiacDance before WildPhoenix's fixed one and owns the remaining BackDmg ordering. " +
                    "ReceiveAttack/Player.cs opens with CombatGear.Update/UpdateTarget, speffect groups 2/3/4/5, " +
                    "then Ninja gouyu_immortals Chance / 100 before the damage modifiers and HP application. " +
                    "MsgAttackPacket.cs dispatches MagicSort.StarArrow to StarArrow.Execute. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: capture one visible exact-D13 StarArrow monster pull and NatureShield player avoidance pair.",
                nextSeam: StarArrowRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.FatalSpin && spellId is 12_110 or 12_240)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile FatalSpin 12110/12240 after source resource/cooldown preparation: " +
                    "the source interaction echo, line geometry, monster/player/SOB physical damage and per-spell scaling, " +
                    "spell progress, and source-shaped animation suppression for empty 12110 versus unconditional 12240 animation. " +
                    "DragonPunch 12240 also models the learned ShadowFist 15410 follow-up where its equipped-rune trigger and target are available. " +
                    "Remaining proof is one visible exact-D13 cast for each base spell and the ShadowFist follow-up.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/FatalSpin.cs handles spells 12110 and 12240; " +
                    "MsgAttackPacket.cs dispatches MagicSort.FatalSpin to FatalSpin.Execute. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: capture visible exact-D13 FatalSpin, DragonPunch, and ShadowFist follow-up casts.",
                nextSeam: FatalSpinRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.RemoveStamin &&
            spellId is 12_830 or 13_070 or 13_080 or 14_280)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile RemoveStamin 12830/13070/13080 FrostGaze and 14280 FrostArrows " +
                    "after source resource/cooldown preparation: battle-power rate gating, Consolidation immunity with the source " +
                    "player-loop break, bounded stamina reduction and update packets, target-point FrostArrows selection, " +
                    "FrostArrows flag 0xDB with duration and RemoveOnDead lifecycle, spell progress, and source-shaped animation targets. " +
                    "Remaining proof is one visible exact-D13 FrostGaze success/miss/immunity set and FrostArrows expiry cast.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/RemoveStamin.cs handles spells 12830, 13070, 13080, and 14280; " +
                    "MsgUpdate.Flags.FrostArrows is 0xDB; MsgAttackPacket.cs dispatches MagicSort.RemoveStamin to RemoveStamin.Execute. " +
                    ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill state owner: capture visible exact-D13 FrostGaze and FrostArrows application/expiry evidence.",
                nextSeam: RemoveStaminRuntimeSeam);
            return true;
        }

        info = default;
        return false;
    }
}
