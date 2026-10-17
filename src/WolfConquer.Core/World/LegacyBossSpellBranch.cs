namespace WolfConquer.Core.World;

public static class LegacyBossSpellBranch
{
    public const ushort DefaultSpellId = 10_000;
    public const ushort FrightenedSpellId = 10_001;
    public const ushort DizzyAreaSpellId = 10_003;
    public const int AreaRadius = 3;
    public const int StatusRatePercent = 5;

    private static readonly uint[] FamilyMonsterIdTable = [7_981U, 20_055U, 40_945U];
    private static readonly ushort[] SpellIdTable = [DefaultSpellId, FrightenedSpellId, DizzyAreaSpellId];

    public static ReadOnlySpan<uint> FamilyMonsterIds => FamilyMonsterIdTable;

    public static ReadOnlySpan<ushort> SpellIds => SpellIdTable;

    public static bool IsFamilyMonsterId(uint monsterId)
    {
        return monsterId is 7_981U or 20_055U or 40_945U;
    }

    public static bool IsSpellId(ushort spellId)
    {
        return spellId is DefaultSpellId or FrightenedSpellId or DizzyAreaSpellId;
    }

    public static bool TryGetSpell(ushort spellId, out LegacyBossSpellBranchSpell spell)
    {
        switch (spellId)
        {
            case DizzyAreaSpellId:
                spell = new LegacyBossSpellBranchSpell(
                    spellId,
                    LegacyBossSpellTargeting.AreaAroundMonster,
                    AreaRadius,
                    LegacyBossSpellDamage.PhysicalAttack,
                    LegacyBossStatusEffect.Dizzy(durationSeconds: 3),
                    HasStatusEffect: true);
                return true;
            case FrightenedSpellId:
                spell = new LegacyBossSpellBranchSpell(
                    spellId,
                    LegacyBossSpellTargeting.SingleTarget,
                    0,
                    LegacyBossSpellDamage.PhysicalAttack,
                    LegacyBossStatusEffect.Frightened(durationSeconds: 6),
                    true);
                return true;
            case DefaultSpellId:
                spell = new LegacyBossSpellBranchSpell(
                    spellId,
                    LegacyBossSpellTargeting.SingleTarget,
                    0,
                    LegacyBossSpellDamage.PhysicalAttack,
                    default,
                    false);
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

public enum LegacyBossSpellTargeting
{
    SingleTarget,
    AreaAroundMonster,
}

public enum LegacyBossSpellDamage
{
    Unknown,
    PhysicalAttack,
}

public readonly record struct LegacyBossSpellBranchSpell(
    ushort SpellId,
    LegacyBossSpellTargeting Targeting,
    int Radius,
    LegacyBossSpellDamage Damage,
    LegacyBossStatusEffect StatusEffect,
    bool HasStatusEffect)
{
    public bool IsAreaOfEffect => Targeting == LegacyBossSpellTargeting.AreaAroundMonster;
}
