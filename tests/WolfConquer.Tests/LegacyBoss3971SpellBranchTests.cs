using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBoss3971SpellBranchTests
{
    [Fact]
    public void FamilyMonsterIdsMatchLegacyBranch()
    {
        Assert.Equal(
            [3_971U, 7_983U, 7_985U, 20_060U, 20_101U, 40_947U, 40_949U],
            LegacyBoss3971SpellBranch.FamilyMonsterIds.ToArray());

        Assert.True(LegacyBoss3971SpellBranch.IsFamilyMonsterId(3_971U));
        Assert.True(LegacyBoss3971SpellBranch.IsFamilyMonsterId(7_983U));
        Assert.True(LegacyBoss3971SpellBranch.IsFamilyMonsterId(7_985U));
        Assert.True(LegacyBoss3971SpellBranch.IsFamilyMonsterId(20_060U));
        Assert.True(LegacyBoss3971SpellBranch.IsFamilyMonsterId(20_101U));
        Assert.True(LegacyBoss3971SpellBranch.IsFamilyMonsterId(40_947U));
        Assert.True(LegacyBoss3971SpellBranch.IsFamilyMonsterId(40_949U));
        Assert.False(LegacyBoss3971SpellBranch.IsFamilyMonsterId(7_981U));
    }

    [Fact]
    public void SpellTablePreservesLegacySelectionOrder()
    {
        Assert.Equal([7_013, 7_014, 7_015, 7_016, 7_017], LegacyBoss3971SpellBranch.SpellIds.ToArray());

        Assert.True(LegacyBoss3971SpellBranch.IsSpellId(7_013));
        Assert.True(LegacyBoss3971SpellBranch.IsSpellId(7_014));
        Assert.True(LegacyBoss3971SpellBranch.IsSpellId(7_015));
        Assert.True(LegacyBoss3971SpellBranch.IsSpellId(7_016));
        Assert.True(LegacyBoss3971SpellBranch.IsSpellId(7_017));
        Assert.False(LegacyBoss3971SpellBranch.IsSpellId(7_018));
    }

    [Theory]
    [InlineData(7_013)]
    [InlineData(7_014)]
    public void FrightenedAreaSpellsAreTargetCenteredRadiusSixPhysical(ushort spellId)
    {
        Assert.True(LegacyBoss3971SpellBranch.TryGetSpell(spellId, out var spell));

        Assert.Equal(spellId, spell.SpellId);
        Assert.Equal(LegacyBoss3971SpellTargeting.TargetCenteredArea, spell.Targeting);
        Assert.True(spell.IsAreaOfEffect);
        Assert.Equal(6, spell.Radius);
        Assert.Equal(LegacyBoss3971SpellAction.PhysicalAttack, spell.Action);
        Assert.Equal(0U, spell.HealAmount);
        Assert.True(spell.HasStatusEffect);
        Assert.Equal(LegacyBossStatusEffect.Frightened(durationSeconds: 3), spell.StatusEffect);
        Assert.Equal(0x36, spell.StatusEffect.StatusFlag);
        Assert.Equal(0x3A, spell.StatusEffect.BlockingFlag);
        Assert.Equal(LegacyGameUpdateDataType.Flustered, spell.StatusEffect.DataType);
    }

    [Fact]
    public void Spell7017IsTargetCenteredRadiusEightPhysicalWithDizzyStatus()
    {
        Assert.True(LegacyBoss3971SpellBranch.TryGetSpell(7_017, out var spell));

        Assert.Equal(7_017, spell.SpellId);
        Assert.Equal(LegacyBoss3971SpellTargeting.TargetCenteredArea, spell.Targeting);
        Assert.True(spell.IsAreaOfEffect);
        Assert.Equal(8, spell.Radius);
        Assert.Equal(LegacyBoss3971SpellAction.PhysicalAttack, spell.Action);
        Assert.Equal(0U, spell.HealAmount);
        Assert.True(spell.HasStatusEffect);
        Assert.Equal(LegacyBossStatusEffect.Dizzy(durationSeconds: 3), spell.StatusEffect);
        Assert.Equal(0x3A, spell.StatusEffect.StatusFlag);
        Assert.Equal(0x36, spell.StatusEffect.BlockingFlag);
        Assert.Equal(LegacyGameUpdateDataType.Stun, spell.StatusEffect.DataType);
    }

    [Fact]
    public void Spell7015IsSingleTargetPhysicalWithDizzyStatus()
    {
        Assert.True(LegacyBoss3971SpellBranch.TryGetSpell(7_015, out var spell));

        Assert.Equal(7_015, spell.SpellId);
        Assert.Equal(LegacyBoss3971SpellTargeting.SingleTarget, spell.Targeting);
        Assert.False(spell.IsAreaOfEffect);
        Assert.Equal(0, spell.Radius);
        Assert.Equal(LegacyBoss3971SpellAction.PhysicalAttack, spell.Action);
        Assert.Equal(0U, spell.HealAmount);
        Assert.True(spell.HasStatusEffect);
        Assert.Equal(LegacyBossStatusEffect.Dizzy(durationSeconds: 3), spell.StatusEffect);
        Assert.Equal(0x3A, spell.StatusEffect.StatusFlag);
        Assert.Equal(0x36, spell.StatusEffect.BlockingFlag);
        Assert.Equal(LegacyGameUpdateDataType.Stun, spell.StatusEffect.DataType);
    }

    [Fact]
    public void Spell7016IsSelfHealForEightyThousandWithoutStatus()
    {
        Assert.True(LegacyBoss3971SpellBranch.TryGetSpell(7_016, out var spell));

        Assert.Equal(7_016, spell.SpellId);
        Assert.Equal(LegacyBoss3971SpellTargeting.Self, spell.Targeting);
        Assert.False(spell.IsAreaOfEffect);
        Assert.Equal(0, spell.Radius);
        Assert.Equal(LegacyBoss3971SpellAction.SelfHeal, spell.Action);
        Assert.Equal(80_000U, spell.HealAmount);
        Assert.False(spell.HasStatusEffect);
        Assert.Equal(default, spell.StatusEffect);
    }

    [Fact]
    public void StatusEffectLookupModelsOnlyStatusSpells()
    {
        Assert.True(LegacyBoss3971SpellBranch.TryGetStatusEffect(7_013, out var frightened));
        Assert.Equal(LegacyBossStatusEffect.Frightened(durationSeconds: 3), frightened);

        Assert.True(LegacyBoss3971SpellBranch.TryGetStatusEffect(7_017, out var dizzy));
        Assert.Equal(LegacyBossStatusEffect.Dizzy(durationSeconds: 3), dizzy);

        Assert.False(LegacyBoss3971SpellBranch.TryGetStatusEffect(7_016, out var healEffect));
        Assert.Equal(default, healEffect);
        Assert.False(LegacyBoss3971SpellBranch.TryGetStatusEffect(7_018, out var unknownEffect));
        Assert.Equal(default, unknownEffect);
    }

    [Fact]
    public void UnknownSpellReturnsDefault()
    {
        Assert.False(LegacyBoss3971SpellBranch.TryGetSpell(7_018, out var spell));

        Assert.Equal(default, spell);
    }
}
