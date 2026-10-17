namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const ushort MatchingHeavensWonderSpellId = 16_350;
    private const string HeavensWonderRuntimeSeam = "heavens-wonder-16350-combat-runtime";

    private static bool TryClassifyCounterKillRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (!IsSupportedCounterKill16350RuntimeSpell(sort, spellId))
        {
            info = default;
            return false;
        }

        info = CreateInfo(
            sort,
            spellId,
            LegacyMagicSortCapability.SpecialCombatDamage,
            LegacyMagicModelingStatus.PartiallyModeled,
            reason:
                "WolfConquer models the matching-decompile direct Heaven's Wonder 16350 cast after source " +
                "resource and cooldown preparation: exact-ID fencing, caster-centered Chebyshev radius six, " +
                "visible monsters before players through the shared attack gates, physical monster damage, " +
                "physical player damage scaled to 70 percent after soul calculation, existing receive/death " +
                "updates, no SOB or progression paths, and an unconditional caster-to-zero animation at the " +
                "query coordinates. The learned-defender 11 percent automatic BackDmg caller is also modeled " +
                "for ordinary direct physical PvP, recursive 16350 player hits, ordinary sort-1 Magic.OnPlayer " +
                "hits, the sort-55 PhysicalSpells three-hit player branch, and EagleEye 11030's primary plus " +
                "unscaled secondary in-line DirectAttack player targets, plus StarArrow 10313's pulled selected-player " +
                "Range.OnPlayer branch, AttackStatus 7000's strict-range visible-player loop, BladeFlurry 11610 and " +
                "Omnipotence 12860's non-XP visible-player " +
                "Range.OnPlayer targets, and " +
                "KineticSpark 11590's clicked Physical.OnPlayer plus strict-range chained Range.OnPlayer targets, " +
                "including source query shape, " +
                "attack-stamp bypass with spell cooldown enforcement, and pre-incoming-hit ordering. Physical " +
                "ShurikenVortex, ManiacDance, and WildPhoenix early returns suppress the proc; PhysicalSpells " +
                "preserves those checks on every hit before its caller-level divide-by-two scaling, while " +
                "EagleEye preserves them before caller scaling: primary-target 14/22 makes source damage one zero, " +
                "while secondary targets remain unscaled at one. StarArrow, AttackStatus 7000, BladeFlurry, Omnipotence, " +
                "and KineticSpark Range.OnPlayer ShurikenVortex/ManiacDance return Base.CalculateExtraAttack from zero " +
                "using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease before WildPhoenix's fixed " +
                "one-damage return, then suppress BackDmg on DefensiveStance's " +
                "40 percent Block return. AttackStatus 7000, BladeFlurry, Omnipotence, and KineticSpark normal eligible " +
                "ranged player hits " +
                "invoke BackDmg before their shared animations; KineticSpark's clicked Physical.OnPlayer hit retains its " +
                "separate source path. " +
                "Magic.OnPlayer " +
                "preserves source WildPhoenix-first ordering before ManiacDance, ShurikenVortex, and MagicDefender " +
                "one-damage early returns, including the MagicDefender Imunity effect. DefensiveStance remains " +
                "eligible. Mapping the remaining specialized Physical.OnPlayer call sites outside the explicitly modeled " +
                "PhysicalSpells, EagleEye, and KineticSpark callers remains debt.",
            sourceEvidence:
                "Matching DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/CounterKill.cs handles " +
                "sort 29 spell 16350 and always sends MsgSpellAnimation after its monster-first and player-second " +
                "loops. Matching AttackHandler.Calculate/BackDmg.cs owns the automatic caller reached from " +
                "Physical.OnPlayer, Magic.OnPlayer, and Range.OnPlayer after their defense early returns. " +
                "Matching AttackHandler/StarArrow.cs routes spell 10313's pulled selected player through Range.OnPlayer. " +
                "Matching AttackHandler/Omnipotence.cs routes non-XP player targets through Range.OnPlayer and sends " +
                "its shared animation after all target loops and progression updates. " +
                "Matching AttackHandler/BladeFlurry.cs routes non-XP player targets through Range.OnPlayer inside inclusive " +
                "Spell.Range and sends its shared animation after all target loops and progression updates. " +
                "Matching AttackHandler/AttackStatus.cs case 7000 routes strict-range players through Range.OnPlayer, " +
                "executes ReceiveAttack.Player before UpdateSpell and one shared animation, and the matching Range.cs " +
                "returns ShurikenVortex/ManiacDance damage through Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease) " +
                "using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease before WildPhoenix's fixed one-damage return, " +
                "then orders DefensiveStance before ranged BackDmg. " +
                "Matching AttackHandler/KineticSpark.cs routes its clicked player through Physical.OnPlayer, chained " +
                "players through Range.OnPlayer inside strict Spell.Range / 2, then ReceiveAttack.Player.Execute before " +
                "one shared animation. " +
                "WolfConquer LegacySpellAnimationPacket supplies the established exact-D13 animation wire.",
            suggestedNextOwner:
                "Receive-side combat owners: classify and opt in the remaining matching specialized Physical.OnPlayer " +
                "and Range.OnPlayer branches outside the explicitly modeled callers without " +
                "turning BackDmg into a universal receive hook.",
            nextSeam: HeavensWonderRuntimeSeam);
        return true;
    }

    public static bool IsSupportedCounterKill16350RuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.CounterKill && spellId == MatchingHeavensWonderSpellId;
    }
}
