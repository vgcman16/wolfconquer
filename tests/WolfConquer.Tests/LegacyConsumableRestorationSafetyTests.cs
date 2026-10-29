using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyConsumableRestorationSafetyTests
{
    [Theory]
    [InlineData(true, 501U, 500U)] [InlineData(false, 301U, 300U)]
    [InlineData(true, 1U, 0U)] [InlineData(false, 1U, 0U)]
    [InlineData(true, uint.MaxValue, uint.MaxValue - 1)]
    [InlineData(false, uint.MaxValue, uint.MaxValue - 1)]
    public void AResourceAlreadyAboveItsCapCannotBeLoweredOrConsumeAnItem(bool health, uint current, uint maximum)
    {
        var result = LegacyConsumableItemUse.Calculate(health ? 1_000_000U : 1_001_000U,
            currentHitpoints: health ? current : 40U, maxHitpoints: health ? maximum : 500U,
            currentMana: health ? 20U : current, maxMana: health ? 300U : maximum,
            itemHitpoints: 70, itemMana: 70, hitpointMultiplierPercent: 200);
        Assert.Equal(LegacyConsumableItemUseOutcome.NoChange, result.Outcome);
        Assert.False(result.ShouldConsumeItem); Assert.False(result.HitpointsChanged); Assert.False(result.ManaChanged);
        Assert.Equal(result.CurrentHitpoints, result.UpdatedHitpoints); Assert.Equal(result.CurrentMana, result.UpdatedMana);
        Assert.Equal(health ? 140U : 0U, result.HitpointAmount); Assert.Equal(health ? 0U : 70U, result.ManaAmount);
    }

    [Theory]
    [InlineData(100U, 500U, 301U, 300U, 500U, 301U)]
    [InlineData(501U, 500U, 40U, 300U, 501U, 300U)]
    [InlineData(501U, 500U, 301U, 300U, 501U, 301U)]
    [InlineData(501U, 500U, 300U, 300U, 501U, 300U)]
    [InlineData(1U, 0U, 1U, 0U, 1U, 1U)]
    [InlineData(uint.MaxValue, uint.MaxValue - 1, 40U, 300U, uint.MaxValue, 300U)]
    public void FullRestoreRetainsOverCapResourcesWhileAllowingTheOtherToImprove(uint hp, uint maxHp,
        uint mana, uint maxMana, uint expectedHp, uint expectedMana)
    {
        // Every case has HP != maximum. HP-full/Mana-low intent is deliberately
        // unresolved here; this regression cannot establish that gameplay rule.
        Assert.NotEqual(hp, maxHp);
        var result = LegacyConsumableItemUse.Calculate(LegacyConsumableItemUse.FullRestoreItemTypeId,
            hp, maxHp, mana, maxMana);
        Assert.Equal(expectedHp, result.UpdatedHitpoints); Assert.Equal(expectedMana, result.UpdatedMana);
        Assert.True(result.UpdatedHitpoints >= hp); Assert.True(result.UpdatedMana >= mana);
        var improved = expectedHp > hp || expectedMana > mana;
        Assert.Equal(improved ? LegacyConsumableItemUseOutcome.Applied : LegacyConsumableItemUseOutcome.NoChange, result.Outcome);
        Assert.Equal(improved, result.ShouldConsumeItem);
        Assert.Equal(expectedHp - hp, result.HitpointAmount); Assert.Equal(expectedMana - mana, result.ManaAmount);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void InRangeRestorationStillUsesItsOriginalClampWithoutUnsignedOverflow(bool health)
    {
        var result = LegacyConsumableItemUse.Calculate(health ? 1_000_000U : 1_001_000U,
            health ? uint.MaxValue - 10 : 40, uint.MaxValue,
            health ? 20 : uint.MaxValue - 10, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        Assert.Equal(LegacyConsumableItemUseOutcome.Applied, result.Outcome); Assert.True(result.ShouldConsumeItem);
        Assert.Equal(health ? uint.MaxValue : 40U, result.UpdatedHitpoints);
        Assert.Equal(health ? 20U : uint.MaxValue, result.UpdatedMana);
        Assert.Equal(health ? uint.MaxValue : 0U, result.HitpointAmount);
        Assert.Equal(health ? 0U : uint.MaxValue, result.ManaAmount);
    }
}
