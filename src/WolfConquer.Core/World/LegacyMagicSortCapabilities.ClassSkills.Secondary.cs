namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const string ShieldBlockRuntimeSeam = "shieldblock-state-runtime";
    private const string OblivionRuntimeSeam = "oblivion-xp-skill-runtime";
    private const string WhirlwindKickRuntimeSeam = "whirlwindkick-combat-runtime";
    private const string MortalDragRuntimeSeam = "mortaldrag-combat-runtime";
    private const string AirKickRuntimeSeam = "airkick-combat-runtime";

    private static bool TryClassifySecondaryClassSkillRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (sort == LegacyMagicSort.ShieldBlock && spellId == 10_470)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile ShieldBlock 10470 after source resource preparation: " +
                    "duration-backed ShieldBlock state, source Damage payload, RemoveOnDead metadata, AzureShield " +
                    "update, self animation, and runtime expiration. Remaining proof is one visible D13 cast/expiry pair.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/ShieldBlock.cs handles sort 52 spell 10470 and sends " +
                    "the status, AzureShield update, and self animation in that order. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill state owner: capture a visible exact-D13 ShieldBlock cast and expiration.",
                nextSeam: ShieldBlockRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.Oblivion && spellId == 10_390)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.XpSkill,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile Oblivion 10390 after source resource preparation: " +
                    "XPList removal, OpenXpSkill replacement/refresh and auto-hunt behavior, 30-second expiry, " +
                    "and the source self-target animation. Remaining proof is one visible D13 cast/expiry pair.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/Oblivion.cs removes XPList, calls " +
                    "Player.OpenXpSkill(Oblivion, 30), and sends the self animation. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill XP owner: capture a visible exact-D13 Oblivion cast and expiration.",
                nextSeam: OblivionRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.WhirlwindKick &&
            spellId is 10_415 or 12_330 or 12_340)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile WhirlwindKick 10415, AirSweep 12330, and AirRaid 12340 " +
                    "after source resource preparation: strict caster-centered radius below five tiles, mixed visible " +
                    "monster/player/SOB physical damage, soul scaling, player 9/14 scaling, source progress, and the " +
                    "source random opponent-field animation. Remaining proof is one visible exact-D13 cast from each player gate.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/WhirlwindKick.cs handles sort 54 spells 10415, 12330, " +
                    "and 12340; 10415 uses CanAttackWhirlwindKick for players while 12330/12340 use CanAttackPlayer. " +
                    ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: capture visible exact-D13 10415 and 12330/12340 radius-damage casts.",
                nextSeam: WhirlwindKickRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.WhirlwindKick && spellId == 13_490)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile WhirlwindKick row 13490 as the source post-resource " +
                    "spell-ID switch fallthrough: source-normalized 2500ms attack stamp, stamina preparation, and no damage or animation.",
                sourceEvidence:
                    "Matching DecompiledSource magictype.txt row 13490 is sort 54 with CoolDown 1500 and stamina 20; " +
                    "MsgAttackPacket.cs normalizes 1001-2999ms attack stamps to 2500ms before dispatch, and matching " +
                    "DecompiledSource AttackHandler/WhirlwindKick.cs has no case 13490. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: confirm the source-silent row with one visible exact-D13 13490 cast.",
                nextSeam: WhirlwindKickRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.MortalDrag && spellId == 11_180)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile MortalDrag 11180 after source resource preparation: " +
                    "visible monster/player pull to the caster before physical damage, monster progress, and the " +
                    "source unconditional post-pull NatureShield return, plus the player ReceiveAttack opening " +
                    "CombatGear/speffect/Ninja envelope before publication. Remaining proof is one visible D13 pull pair.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/MortalDrag.cs handles sort 65 spell 11180 and moves " +
                    "the selected monster/player before damage; equipped NatureShield returns after the player pull. " +
                    "Its accepted player branch then applies Physical.OnPlayer plus Base.CalculateSoul, calls " +
                    "ReceiveAttack.Player.Execute, and queues the target. Matching ReceiveAttack/Player.cs opens with " +
                    "attacker CombatGear.Update, defender CombatGear.UpdateTarget, attacker speffect group 2, defender " +
                    "speffect groups 3/4/5, and Ninja SageMode using gouyu Chance / 100. " +
                    ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: capture a visible exact-D13 MortalDrag monster/player pull pair.",
                nextSeam: MortalDragRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.AirKick && spellId == 12_320)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile AirKick 12320 after source resource preparation: " +
                    "floor-safe path movement, Guild War endpoint rejection, width-one path targets, player 13/5 " +
                    "damage, the player ReceiveAttack opening CombatGear/speffect/Ninja envelope before publication, " +
                    "NextSpell 12330, source SOB progress accumulation, and progress before animation. " +
                    "Remaining proof is one visible D13 movement/damage cast.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/AirKick.cs handles sort 79 spell 12320, moves the caster " +
                    "along CheckBladeTeampsCoords, applies Physical.OnPlayer plus Base.CalculateSoul then * 13 / 5, " +
                    "calls ReceiveAttack.Player.Execute before queuing each accepted player, and sends NextSpell 12330 " +
                    "through MsgSpellAnimation. Matching ReceiveAttack/Player.cs opens with attacker CombatGear.Update, " +
                    "defender CombatGear.UpdateTarget, attacker speffect group 2, defender speffect groups 3/4/5, and " +
                    "Ninja SageMode using gouyu Chance / 100. " +
                    ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: capture a visible exact-D13 AirKick movement/damage cast.",
                nextSeam: AirKickRuntimeSeam);
            return true;
        }

        info = default;
        return false;
    }
}
