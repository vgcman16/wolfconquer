using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicResourceCostCalculatorTests
{
    [Theory]
    [InlineData(1000, 1052)]
    [InlineData(2000, 2052)]
    [InlineData(4000, 4052)]
    [InlineData(5000, 5052)]
    [InlineData(6000, 6052)]
    [InlineData(7000, 7052)]
    [InlineData(8000, 8050)]
    [InlineData(9000, 9052)]
    [InlineData(13002, 13052)]
    [InlineData(14002, 14052)]
    [InlineData(16000, 16052)]
    public void MpMasterRuneRejectsClassImmediatelyAboveMatchingSourceJobFamily(
        int requiredClass,
        long characterClass)
    {
        // Matching ItemType.EquipPassJobReq caps most families at *51,
        // but the 8000 family at 8049; a broader *57 rule is not this source.
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 103,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)", checked((uint)requiredClass)),
            CreateLegacyRuneEligibleAttributes(),
            characterClass: characterClass);

        Assert.Equal(100, cost.UseMana);
        Assert.False(cost.HasMpMaster);
        Assert.Null(cost.MpMasterLevel);
        Assert.Equal(0, cost.MpMasterReductionPercent);
    }

    [Theory]
    [InlineData(132, true, 10)]
    [InlineData(133, true, 10)]
    [InlineData(134, true, 10)]
    [InlineData(132, false, 100)]
    [InlineData(133, false, 100)]
    [InlineData(134, false, 100)]
    public void MpMasterRuneUsesMatchingSourceFinalYellowAlternateSlotsOnlyWhenActive(
        ushort position,
        bool alternateEquipmentActive,
        ushort expectedMana)
    {
        // ItemType.isRuneCheck permits yellow alternate slots123..134;
        // Rune.WornObjects excludes them while the normal set is active.
        var runeItemId = CreateMpMasterRuneItemId(9);
        var cost = LegacyMagicResourceCostCalculator.Calculate(
            CreateSpell(useMana: 100),
            CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = position,
            }),
            CreateItemCatalog((int)runeItemId, "MPMaster(+9)"),
            CreateLegacyRuneEligibleAttributes(),
            alternateEquipmentActive: alternateEquipmentActive);

        Assert.Equal(expectedMana, cost.UseMana);
        Assert.Equal(alternateEquipmentActive, cost.HasMpMaster);
        Assert.Equal(alternateEquipmentActive ? (byte?)9 : null, cost.MpMasterLevel);
    }
}
