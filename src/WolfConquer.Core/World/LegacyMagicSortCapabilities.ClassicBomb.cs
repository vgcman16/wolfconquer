namespace WolfConquer.Core.World;

public static partial class LegacyMagicSortCapabilities
{
    private const ushort MatchingSnowSpellId = 5_010;
    private const ushort MatchingRageSpellId = 7_020;
    private const ushort MatchingArrowRainSpellId = 8_030;
    private const string ClassicBombRangeRuntimeSeam = "classic-bomb-range-runtime";

    private static bool TryClassifyClassicBombRangeRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        out LegacyMagicSortCapabilityInfo info)
    {
        if (!IsSupportedClassicBombRangeRuntimeSpell(sort, spellId))
        {
            info = default;
            return false;
        }

        var isArrowRain = spellId == MatchingArrowRainSpellId;
        info = CreateInfo(
            sort,
            spellId,
            LegacyMagicSortCapability.SpecialCombatDamage,
            LegacyMagicModelingStatus.PartiallyModeled,
            reason:
                "WolfConquer models the matching-decompile classic Bomb runtime for Snow 5010, Rage 7020, " +
                "and ArrowRain 8030 after source resource preparation: exact-ID fencing, caster-centered strict " +
                "distance under Spell.Range, monster then player then SOB target order, physical range damage, " +
                "Range.OnPlayer ShurikenVortex and ManiacDance zero-base extra-attack return before WildPhoenix, " +
                "DefensiveStance before ranged BackDmg, player HP updates before one shared animation, and " +
                "monster/SOB spell and character progress without weapon proficiency." +
                (isArrowRain
                    ? " ArrowRain also removes XPList and publishes its scalar and status-vector updates before damage and animation."
                    : string.Empty) +
                " Remaining parity is exact shared CanAttack side-effect review and one visible exact-D13 cast per spell.",
            sourceEvidence:
                "Matching DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/Bomb.cs cases 5010, " +
                "7020, and 8030 define the resource, target, progress, XPList, and animation ordering. Matching " +
                "AttackHandler.Calculate/Range.cs defines the player status-return, DefensiveStance, and BackDmg " +
                "precedence. Matching DBServer/magictype.txt identifies all three as Bomb sort 5. WolfConquer's " +
                "LegacySpellAnimationPacket and MsgUpdate builders provide the established exact-D13 wire; no " +
                "Source7770 opcode or gameplay assumptions are used.",
            suggestedNextOwner:
                "Classic Bomb combat owner: review the remaining shared CanAttack side effects and capture visible " +
                "exact-D13 Snow, Rage, and ArrowRain casts.",
            nextSeam: ClassicBombRangeRuntimeSeam);
        return true;
    }

    public static bool IsSupportedClassicBombRangeRuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Bomb &&
            spellId is MatchingSnowSpellId or MatchingRageSpellId or MatchingArrowRainSpellId;
    }
}
