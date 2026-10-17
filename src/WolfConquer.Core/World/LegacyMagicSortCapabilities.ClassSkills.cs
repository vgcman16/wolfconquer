namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const string BurntFrostRuntimeSeam = "burntfrost-combat-runtime";
    private const string SwirlingStormRuntimeSeam = "swirlingstorm-combat-runtime";
    private const string PitchingRuntimeSeam = "pitching-state-runtime";
    private const string HeavensWrathRuntimeSeam = "heavenswrath-combat-runtime";

    private const string ClassSkillWireEvidence =
        "Exact D13 client client\\Env_DX8\\._cache_Conquer.exe SHA-256 " +
        "D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5 " +
        "remains wire authority; these runtimes reuse the existing D13 MsgAttack and MsgSpellAnimation packet builders.";

    private static bool TryClassifyClassSkillRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (sort == LegacyMagicSort.BurntFrost && spellId == 12_940)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile BurntFrost 12940 after source resource/cooldown preparation: " +
                    "caster-centered range-7 width-180 fan selection with the source distance < 3 fallback, " +
                    "monster/player/SOB physical damage, player * 5 / 7 scaling with animation Hit=0, " +
                    "the player ReceiveAttack opening CombatGear/speffect/Ninja envelope before publication, and " +
                    "experience and spell progress before the final animation. Remaining proof is exact shared " +
                    "CanAttack side-effect coverage and one visible D13 cast.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/BurntFrost.cs handles only spell 12940; " +
                    "its accepted player branch applies Physical.OnPlayer plus Base.CalculateSoul then * 5 / 7, calls " +
                    "ReceiveAttack.Player.Execute, sets Hit=0, and queues the target. Matching ReceiveAttack/Player.cs " +
                    "opens with attacker CombatGear.Update, defender CombatGear.UpdateTarget, attacker speffect group 2, " +
                    "defender speffect groups 3/4/5, and Ninja SageMode using gouyu Chance / 100. MsgAttackPacket.cs " +
                    "dispatches MagicSort.BurntFrost to BurntFrost.Execute. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: tighten shared CanAttack side effects and capture one visible exact-D13 BurntFrost cast.",
                nextSeam: BurntFrostRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.SwirlingStorm && spellId == 12_890)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile SwirlingStorm 12890 after source resource/cooldown preparation: " +
                    "Omnipotence stamina +10 behavior, width-2 increased-range line selection, monster/player/SOB damage, " +
                    "player * 9 / 15 and SOB * 40 / 100 scaling, the player ReceiveAttack opening CombatGear/speffect/Ninja " +
                    "envelope before publication, player animation Hit=0, and progress before animation. " +
                    "Remaining proof is exact shared CanAttack side-effect coverage and one visible D13 cast.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/SwirlingStorm.cs handles only spell 12890; " +
                    "its accepted player branch applies Physical.OnPlayer plus Base.CalculateSoul then * 9 / 15, calls " +
                    "ReceiveAttack.Player.Execute, sets Hit=0, and queues the target. Matching ReceiveAttack/Player.cs " +
                    "opens with attacker CombatGear.Update, defender CombatGear.UpdateTarget, attacker speffect group 2, " +
                    "defender speffect groups 3/4/5, and Ninja SageMode using gouyu Chance / 100. " +
                    "MsgAttackPacket.cs dispatches MagicSort.SwirlingStorm to SwirlingStorm.Execute. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: tighten shared CanAttack side effects and capture one visible exact-D13 SwirlingStorm cast.",
                nextSeam: SwirlingStormRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.Pitching && spellId == 14_270)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile Pitching 14270 after source resource/cooldown preparation: " +
                    "visible-player-only targeting, target-centered animation, the source battle-power success roll, " +
                    "current XP-skill and XPList removal plus XPCount reset on success, and Hit=0 on failure. " +
                    "Remaining proof is exact Core.Rate boundary capture and one visible D13 success/failure pair.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/Pitching.cs handles only spell 14270; " +
                    "MsgAttackPacket.cs dispatches MagicSort.Pitching to Pitching.Execute. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill state owner: capture a visible exact-D13 Pitching success/failure pair and XP-skill removal.",
                nextSeam: PitchingRuntimeSeam);
            return true;
        }

        if (sort == LegacyMagicSort.HeavensWrath && spellId == 15_710)
        {
            info = CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models matching-decompile HeavensWrath 15710 after source resource/cooldown preparation: " +
                    "ThunderStriker class gating, Ride removal, first-cast HeavensWrath and Fly duration flags after the " +
                    "self animation, and active recast radius <= 7 monster/player/SOB physical damage with spell effect 1, " +
                    "the player ReceiveAttack opening CombatGear/speffect/Ninja envelope before publication, progress, and " +
                    "animation. Remaining proof is exact shared CanAttack side effects and a visible D13 first/recast pair.",
                sourceEvidence:
                    "Matching DecompiledSource AttackHandler/HeavensWrath.cs handles only spell 15710; " +
                    "its active-recast player branch applies Physical.OnPlayer plus Base.CalculateSoul, calls " +
                    "ReceiveAttack.Player.Execute, and queues the target. Matching ReceiveAttack/Player.cs opens with " +
                    "attacker CombatGear.Update, defender CombatGear.UpdateTarget, attacker speffect group 2, defender " +
                    "speffect groups 3/4/5, and Ninja SageMode using gouyu Chance / 100. " +
                    "MsgAttackPacket.cs dispatches MagicSort.HeavensWrath to HeavensWrath.Execute. " + ClassSkillWireEvidence,
                suggestedNextOwner:
                    "Class-skill combat owner: tighten shared CanAttack side effects and capture a visible exact-D13 HeavensWrath first/recast pair.",
                nextSeam: HeavensWrathRuntimeSeam);
            return true;
        }

        info = default;
        return false;
    }
}
