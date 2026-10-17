using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBossStatusEffectTests
{
    [Theory]
    [InlineData(7_013)]
    [InlineData(7_014)]
    [InlineData(10_362)]
    [InlineData(10_531)]
    public void ModeledFrightenedBossSpellsReturnSourceBackedEffect(ushort spellId)
    {
        Assert.True(LegacyBossStatusEffect.TryGetForSpell(spellId, out var effect));

        Assert.Equal(0x36, effect.StatusFlag);
        Assert.Equal(0x3A, effect.BlockingFlag);
        Assert.Equal(3U, effect.DurationSeconds);
        Assert.Equal(LegacyGameUpdateDataType.Flustered, effect.DataType);
        Assert.Equal(54U, (uint)effect.DataType);
    }

    [Theory]
    [InlineData(7_015)]
    [InlineData(7_017)]
    [InlineData(11_322)]
    [InlineData(30_013)]
    [InlineData(30_014)]
    public void ModeledDizzyBossSpellsReturnSourceBackedEffect(ushort spellId)
    {
        Assert.True(LegacyBossStatusEffect.TryGetForSpell(spellId, out var effect));

        Assert.Equal(0x3A, effect.StatusFlag);
        Assert.Equal(0x36, effect.BlockingFlag);
        Assert.Equal(3U, effect.DurationSeconds);
        Assert.Equal(LegacyGameUpdateDataType.Stun, effect.DataType);
        Assert.Equal(58U, (uint)effect.DataType);
    }

    [Theory]
    [InlineData(8_003)]
    [InlineData(11_311)]
    [InlineData(10_364)]
    [InlineData(7_016)]
    [InlineData(10_372)]
    [InlineData(10_500)]
    [InlineData(10_502)]
    public void ModeledBossSpellsWithoutSourceStatusEffectReturnFalse(ushort spellId)
    {
        Assert.False(LegacyBossStatusEffect.TryGetForSpell(spellId, out var effect));

        Assert.Equal(default, effect);
    }
}
