using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossSpellBranchTests
{
    [Fact]
    public void FamilyMonsterIdsMatchLegacyBranch()
    {
        Assert.Equal([7_981U, 20_055U, 40_945U], LegacyBossSpellBranch.FamilyMonsterIds.ToArray());

        Assert.True(LegacyBossSpellBranch.IsFamilyMonsterId(7_981U));
        Assert.True(LegacyBossSpellBranch.IsFamilyMonsterId(20_055U));
        Assert.True(LegacyBossSpellBranch.IsFamilyMonsterId(40_945U));
        Assert.False(LegacyBossSpellBranch.IsFamilyMonsterId(7_980U));
    }

    [Fact]
    public void SpellTablePreservesLegacySelectionOrder()
    {
        Assert.Equal([10_000, 10_001, 10_003], LegacyBossSpellBranch.SpellIds.ToArray());

        Assert.True(LegacyBossSpellBranch.IsSpellId(10_000));
        Assert.True(LegacyBossSpellBranch.IsSpellId(10_001));
        Assert.True(LegacyBossSpellBranch.IsSpellId(10_003));
        Assert.False(LegacyBossSpellBranch.IsSpellId(10_002));
    }

    [Fact]
    public void Spell10003IsAreaAroundMonsterWithRadiusThreeAndDizzyStatus()
    {
        Assert.True(LegacyBossSpellBranch.TryGetSpell(10_003, out var spell));

        Assert.Equal(10_003, spell.SpellId);
        Assert.Equal(LegacyBossSpellTargeting.AreaAroundMonster, spell.Targeting);
        Assert.True(spell.IsAreaOfEffect);
        Assert.Equal(3, spell.Radius);
        Assert.Equal(LegacyBossSpellDamage.PhysicalAttack, spell.Damage);
        Assert.Equal(5, LegacyBossSpellBranch.StatusRatePercent);
        Assert.True(spell.HasStatusEffect);
        Assert.Equal(0x3A, spell.StatusEffect.StatusFlag);
        Assert.Equal(0x36, spell.StatusEffect.BlockingFlag);
        Assert.Equal(3U, spell.StatusEffect.DurationSeconds);
        Assert.Equal(LegacyGameUpdateDataType.Stun, spell.StatusEffect.DataType);
    }

    [Fact]
    public void Spell10001IsSingleTargetWithFrightenedStatus()
    {
        Assert.True(LegacyBossSpellBranch.TryGetSpell(10_001, out var spell));

        Assert.Equal(10_001, spell.SpellId);
        Assert.Equal(LegacyBossSpellTargeting.SingleTarget, spell.Targeting);
        Assert.False(spell.IsAreaOfEffect);
        Assert.Equal(0, spell.Radius);
        Assert.Equal(LegacyBossSpellDamage.PhysicalAttack, spell.Damage);
        Assert.Equal(5, LegacyBossSpellBranch.StatusRatePercent);
        Assert.True(spell.HasStatusEffect);
        Assert.Equal(0x36, spell.StatusEffect.StatusFlag);
        Assert.Equal(0x3A, spell.StatusEffect.BlockingFlag);
        Assert.Equal(6U, spell.StatusEffect.DurationSeconds);
        Assert.Equal(LegacyGameUpdateDataType.Flustered, spell.StatusEffect.DataType);
    }

    [Fact]
    public void Spell10000IsSingleTargetWithoutStatus()
    {
        Assert.True(LegacyBossSpellBranch.TryGetSpell(10_000, out var spell));

        Assert.Equal(10_000, spell.SpellId);
        Assert.Equal(LegacyBossSpellTargeting.SingleTarget, spell.Targeting);
        Assert.False(spell.IsAreaOfEffect);
        Assert.Equal(0, spell.Radius);
        Assert.Equal(LegacyBossSpellDamage.PhysicalAttack, spell.Damage);
        Assert.False(spell.HasStatusEffect);
        Assert.Equal(default, spell.StatusEffect);
        Assert.False(LegacyBossSpellBranch.TryGetStatusEffect(10_000, out var effect));
        Assert.Equal(default, effect);
    }

    [Fact]
    public void StatusEffectLookupModelsOnlyStatusSpells()
    {
        Assert.True(LegacyBossSpellBranch.TryGetStatusEffect(10_001, out var frightened));
        Assert.Equal(0x36, frightened.StatusFlag);
        Assert.Equal(6U, frightened.DurationSeconds);

        Assert.True(LegacyBossSpellBranch.TryGetStatusEffect(10_003, out var dizzy));
        Assert.Equal(0x3A, dizzy.StatusFlag);
        Assert.Equal(3U, dizzy.DurationSeconds);

        Assert.False(LegacyBossSpellBranch.TryGetStatusEffect(10_002, out var unknown));
        Assert.Equal(default, unknown);
        Assert.False(LegacyBossSpellBranch.TryGetSpell(10_002, out var spell));
        Assert.Equal(default, spell);
    }
}
