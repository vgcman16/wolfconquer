using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyConsumableItemUseTests
{
    [Theory]
    [InlineData(1_000_000U)]
    [InlineData(1_000_040U)]
    [InlineData(1_002_000U)]
    [InlineData(1_002_029U)]
    [InlineData(1_002_050U)]
    [InlineData(725_065U)]
    [InlineData(1_003_010U)]
    public void ClassifyRecognizesLegacyHealthConsumableRanges(uint itemTypeId)
    {
        Assert.Equal(LegacyConsumableItemKind.Hitpoints, LegacyConsumableItemUse.Classify(itemTypeId));
        Assert.True(LegacyConsumableItemUse.IsLegacyHealthConsumable(itemTypeId));
        Assert.False(LegacyConsumableItemUse.IsLegacyManaConsumable(itemTypeId));
    }

    [Theory]
    [InlineData(1_001_000U)]
    [InlineData(1_001_040U)]
    [InlineData(1_002_030U)]
    [InlineData(1_002_040U)]
    [InlineData(725_066U)]
    [InlineData(1_004_010U)]
    public void ClassifyRecognizesLegacyManaConsumableRanges(uint itemTypeId)
    {
        Assert.Equal(LegacyConsumableItemKind.Mana, LegacyConsumableItemUse.Classify(itemTypeId));
        Assert.True(LegacyConsumableItemUse.IsLegacyManaConsumable(itemTypeId));
        Assert.False(LegacyConsumableItemUse.IsLegacyHealthConsumable(itemTypeId));
    }

    [Fact]
    public void HealthConsumableRestoresHitpointsOnly()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_000_000,
            currentHitpoints: 40,
            maxHitpoints: 500,
            currentMana: 20,
            maxMana: 300,
            itemHitpoints: 70);

        Assert.Equal(LegacyConsumableItemKind.Hitpoints, result.Kind);
        Assert.Equal(LegacyConsumableItemUseOutcome.Applied, result.Outcome);
        Assert.True(result.ShouldConsumeItem);
        Assert.True(result.HitpointsChanged);
        Assert.False(result.ManaChanged);
        Assert.Equal(110U, result.UpdatedHitpoints);
        Assert.Equal(20U, result.UpdatedMana);
        Assert.Equal(70U, result.HitpointAmount);
        Assert.Equal(0U, result.ManaAmount);
    }

    [Fact]
    public void ManaConsumableRestoresManaOnly()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_001_000,
            currentHitpoints: 400,
            maxHitpoints: 500,
            currentMana: 40,
            maxMana: 5_000,
            itemMana: 70);

        Assert.Equal(LegacyConsumableItemKind.Mana, result.Kind);
        Assert.Equal(LegacyConsumableItemUseOutcome.Applied, result.Outcome);
        Assert.False(result.HitpointsChanged);
        Assert.True(result.ManaChanged);
        Assert.Equal(400U, result.UpdatedHitpoints);
        Assert.Equal(110U, result.UpdatedMana);
        Assert.Equal(0U, result.HitpointAmount);
        Assert.Equal(70U, result.ManaAmount);
    }

    [Fact]
    public void FullRestoreRestoresHitpointsAndManaWhenHitpointsAreMissing()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: LegacyConsumableItemUse.FullRestoreItemTypeId,
            currentHitpoints: 100,
            maxHitpoints: 500,
            currentMana: 40,
            maxMana: 300);

        Assert.Equal(LegacyConsumableItemKind.FullRestore, result.Kind);
        Assert.Equal(LegacyConsumableItemUseOutcome.Applied, result.Outcome);
        Assert.False(result.RequiresItemTypeAmounts);
        Assert.True(result.HitpointsChanged);
        Assert.True(result.ManaChanged);
        Assert.Equal(500U, result.UpdatedHitpoints);
        Assert.Equal(300U, result.UpdatedMana);
        Assert.Equal(400U, result.HitpointAmount);
        Assert.Equal(260U, result.ManaAmount);
    }

    [Fact]
    public void HealthConsumableClampsToMaxAndKeepsLegacyItemAmount()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_000_040,
            currentHitpoints: 95,
            maxHitpoints: 100,
            currentMana: 20,
            maxMana: 300,
            itemHitpoints: 70);

        Assert.Equal(LegacyConsumableItemUseOutcome.Applied, result.Outcome);
        Assert.Equal(100U, result.UpdatedHitpoints);
        Assert.Equal(70U, result.HitpointAmount);
    }

    [Fact]
    public void HealthConsumableAppliesLegacyStatusMultiplierBeforeClamp()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_000_000,
            currentHitpoints: 40,
            maxHitpoints: 500,
            currentMana: 20,
            maxMana: 300,
            itemHitpoints: 70,
            hitpointMultiplierPercent: 200);

        Assert.Equal(LegacyConsumableItemUseOutcome.Applied, result.Outcome);
        Assert.Equal(180U, result.UpdatedHitpoints);
        Assert.Equal(140U, result.HitpointAmount);
    }

    [Fact]
    public void HealthConsumableAlreadyFullDoesNotConsume()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_000_000,
            currentHitpoints: 500,
            maxHitpoints: 500,
            currentMana: 20,
            maxMana: 300,
            itemHitpoints: 70);

        Assert.Equal(LegacyConsumableItemUseOutcome.NoChange, result.Outcome);
        Assert.False(result.ShouldConsumeItem);
        Assert.False(result.HitpointsChanged);
        Assert.Equal(500U, result.UpdatedHitpoints);
        Assert.Equal(20U, result.UpdatedMana);
    }

    [Fact]
    public void FullRestoreMatchesHandlerNoOpWhenHitpointsAreAlreadyFull()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: LegacyConsumableItemUse.FullRestoreItemTypeId,
            currentHitpoints: 500,
            maxHitpoints: 500,
            currentMana: 40,
            maxMana: 300);

        Assert.Equal(LegacyConsumableItemUseOutcome.NoChange, result.Outcome);
        Assert.False(result.ShouldConsumeItem);
        Assert.False(result.HitpointsChanged);
        Assert.False(result.ManaChanged);
        Assert.Equal(40U, result.UpdatedMana);
    }

    [Fact]
    public void UnknownItemIsRejected()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: 999_999,
            currentHitpoints: 40,
            maxHitpoints: 500,
            currentMana: 20,
            maxMana: 300,
            itemHitpoints: 70,
            itemMana: 70);

        Assert.Equal(LegacyConsumableItemKind.Unknown, result.Kind);
        Assert.Equal(LegacyConsumableItemUseOutcome.Rejected, result.Outcome);
        Assert.False(result.IsRecognized);
        Assert.False(result.ShouldConsumeItem);
        Assert.Equal(40U, result.UpdatedHitpoints);
        Assert.Equal(20U, result.UpdatedMana);
    }

    [Fact]
    public void HealthConsumableWithZeroMaxDoesNotChange()
    {
        var result = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_000_000,
            currentHitpoints: 0,
            maxHitpoints: 0,
            currentMana: 20,
            maxMana: 300,
            itemHitpoints: 70);

        Assert.Equal(LegacyConsumableItemUseOutcome.NoChange, result.Outcome);
        Assert.False(result.ShouldConsumeItem);
        Assert.Equal(0U, result.UpdatedHitpoints);
        Assert.Equal(70U, result.HitpointAmount);
    }

    [Fact]
    public void MissingLegacyItemAmountDoesNotApply()
    {
        var healthResult = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_000_000,
            currentHitpoints: 40,
            maxHitpoints: 500,
            currentMana: 20,
            maxMana: 300);
        var manaResult = LegacyConsumableItemUse.Calculate(
            itemTypeId: 1_001_000,
            currentHitpoints: 40,
            maxHitpoints: 500,
            currentMana: 20,
            maxMana: 300);

        Assert.Equal(LegacyConsumableItemUseOutcome.MissingHitpointAmount, healthResult.Outcome);
        Assert.Equal(LegacyConsumableItemUseOutcome.MissingManaAmount, manaResult.Outcome);
        Assert.False(healthResult.ShouldConsumeItem);
        Assert.False(manaResult.ShouldConsumeItem);
    }
}
