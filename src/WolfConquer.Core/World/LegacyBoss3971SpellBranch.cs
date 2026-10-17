namespace WolfConquer.Core.World;

public static class LegacyBoss3971SpellBranch
{
    public const ushort FrightenedAreaSpellId = 7_013;
    public const ushort FrightenedAreaAlternateSpellId = 7_014;
    public const ushort DizzySingleTargetSpellId = 7_015;
    public const ushort SelfHealSpellId = 7_016;
    public const ushort DizzyAreaSpellId = 7_017;

    public const int FrightenedAreaRadius = 6;
    public const int DizzyAreaRadius = 8;
    public const uint SelfHealAmount = 80_000U;

    private static readonly uint[] FamilyMonsterIdTable = [3_971U, 7_983U, 7_985U, 20_060U, 20_101U, 40_947U, 40_949U];
    private static readonly ushort[] SpellIdTable =
    [
        FrightenedAreaSpellId,
        FrightenedAreaAlternateSpellId,
        DizzySingleTargetSpellId,
        SelfHealSpellId,
        DizzyAreaSpellId,
    ];

    public static ReadOnlySpan<uint> FamilyMonsterIds => FamilyMonsterIdTable;

    public static ReadOnlySpan<ushort> SpellIds => SpellIdTable;

    public static bool IsFamilyMonsterId(uint monsterId)
    {
        return monsterId is 3_971U or 7_983U or 7_985U or 20_060U or 20_101U or 40_947U or 40_949U;
    }

    public static bool IsSpellId(ushort spellId)
    {
        return spellId is FrightenedAreaSpellId
            or FrightenedAreaAlternateSpellId
            or DizzySingleTargetSpellId
            or SelfHealSpellId
            or DizzyAreaSpellId;
    }

    public static bool TryGetSpell(ushort spellId, out LegacyBoss3971SpellBranchSpell spell)
    {
        switch (spellId)
        {
            case FrightenedAreaSpellId:
            case FrightenedAreaAlternateSpellId:
                spell = new LegacyBoss3971SpellBranchSpell(
                    spellId,
                    LegacyBoss3971SpellTargeting.TargetCenteredArea,
                    FrightenedAreaRadius,
                    LegacyBoss3971SpellAction.PhysicalAttack,
                    0,
                    LegacyBossStatusEffect.Frightened(durationSeconds: 3),
                    HasStatusEffect: true);
                return true;
            case DizzySingleTargetSpellId:
                spell = new LegacyBoss3971SpellBranchSpell(
                    spellId,
                    LegacyBoss3971SpellTargeting.SingleTarget,
                    0,
                    LegacyBoss3971SpellAction.PhysicalAttack,
                    0,
                    LegacyBossStatusEffect.Dizzy(durationSeconds: 3),
                    HasStatusEffect: true);
                return true;
            case SelfHealSpellId:
                spell = new LegacyBoss3971SpellBranchSpell(
                    spellId,
                    LegacyBoss3971SpellTargeting.Self,
                    0,
                    LegacyBoss3971SpellAction.SelfHeal,
                    SelfHealAmount,
                    default,
                    HasStatusEffect: false);
                return true;
            case DizzyAreaSpellId:
                spell = new LegacyBoss3971SpellBranchSpell(
                    spellId,
                    LegacyBoss3971SpellTargeting.TargetCenteredArea,
                    DizzyAreaRadius,
                    LegacyBoss3971SpellAction.PhysicalAttack,
                    0,
                    LegacyBossStatusEffect.Dizzy(durationSeconds: 3),
                    HasStatusEffect: true);
                return true;
            default:
                spell = default;
                return false;
        }
    }

    public static bool TryGetStatusEffect(ushort spellId, out LegacyBossStatusEffect effect)
    {
        if (TryGetSpell(spellId, out var spell) && spell.HasStatusEffect)
        {
            effect = spell.StatusEffect;
            return true;
        }

        effect = default;
        return false;
    }
}

public enum LegacyBoss3971SpellTargeting
{
    Unknown,
    SingleTarget,
    TargetCenteredArea,
    Self,
}

public enum LegacyBoss3971SpellAction
{
    Unknown,
    PhysicalAttack,
    SelfHeal,
}

public readonly record struct LegacyBoss3971SpellBranchSpell(
    ushort SpellId,
    LegacyBoss3971SpellTargeting Targeting,
    int Radius,
    LegacyBoss3971SpellAction Action,
    uint HealAmount,
    LegacyBossStatusEffect StatusEffect,
    bool HasStatusEffect)
{
    public bool IsAreaOfEffect => Targeting == LegacyBoss3971SpellTargeting.TargetCenteredArea;
}
