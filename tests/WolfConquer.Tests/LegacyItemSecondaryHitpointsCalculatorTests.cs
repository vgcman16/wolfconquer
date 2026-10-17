using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyItemSecondaryHitpointsCalculatorTests
{
    [Fact]
    public void CalculateSumsHpAddRelicAttributesFromDurableEquippedRelic()
    {
        var playerItems = Snapshot(
            equipment:
            [
                Item(
                    itemId: 4_100_001,
                    position: 13,
                    durability: 1,
                    attribute1: EncodeRelicAttribute(LegacyChiAttributeType.HPAdd, 183),
                    attribute2: EncodeRelicAttribute(LegacyChiAttributeType.AddAttack, 650),
                    attribute3: EncodeRelicAttribute(LegacyChiAttributeType.HPAdd, 100)),
            ]);

        var contribution = LegacyItemSecondaryHitpointsCalculator.Calculate(
            playerItems,
            EmptySpecialEffectRows(),
            EmptyRefineryRows());

        Assert.Equal(283U, contribution.RelicAttributeHitpoints);
        Assert.Equal(283U, contribution.Total);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateAddsInLifeIntensificationRefineryFromRifineryRow()
    {
        var playerItems = Snapshot(
            equipment:
            [
                Item(
                    itemId: 144_309,
                    position: 1,
                    durability: 1,
                    refineryEffectId: 724_440),
            ]);
        var refineryRows = new Dictionary<int, RefineryMaterial>
        {
            [724_440] = RefineryMaterial(
                itemType: 724_440,
                primaryValue: 200,
                effectType: 10,
                secondaryType: 10,
                secondaryValue: 83),
        };

        var contribution = LegacyItemSecondaryHitpointsCalculator.Calculate(
            playerItems,
            EmptySpecialEffectRows(),
            refineryRows);

        Assert.Equal(283U, contribution.RefineryHitpoints);
        Assert.Equal(283U, contribution.Total);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateAddsMythsoulBagMaxHpFromSpecialEffectAttributeRow()
    {
        var playerItems = Snapshot(
            mythsoulBag:
            [
                Item(itemId: 40_701_016, position: 217, durability: 1),
                Item(itemId: 40_702_026, position: 217, durability: 1),
            ]);
        var specialEffectRows = new Dictionary<int, SpecialEffectAttribute>
        {
            [40_701_016] = SpecialEffectAttribute(itemId: 40_701_016, type: 1, value: 100),
            [40_702_026] = SpecialEffectAttribute(itemId: 40_702_026, type: 12, value: 100),
        };

        var contribution = LegacyItemSecondaryHitpointsCalculator.Calculate(
            playerItems,
            specialEffectRows,
            EmptyRefineryRows());

        Assert.Equal(100U, contribution.MythsoulBagHitpoints);
        Assert.Equal(100U, contribution.Total);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateSkipsZeroDurabilityEquipmentAndExpiredRefinery()
    {
        var now = new DateTime(2026, 6, 5, 12, 0, 0);
        var playerItems = Snapshot(
            equipment:
            [
                Item(
                    itemId: 4_100_001,
                    position: 13,
                    durability: 0,
                    attribute1: EncodeRelicAttribute(LegacyChiAttributeType.HPAdd, 283)),
                Item(
                    itemId: 144_309,
                    position: 1,
                    durability: 1,
                    refineryEffectId: 724_440,
                    refineryEffectDuration: 60,
                    refineryEffectAddedOn: now.AddMinutes(-2).ToBinary()),
            ]);
        var refineryRows = new Dictionary<int, RefineryMaterial>
        {
            [724_440] = RefineryMaterial(
                itemType: 724_440,
                primaryValue: 283,
                effectType: 10),
        };

        var contribution = LegacyItemSecondaryHitpointsCalculator.Calculate(
            playerItems,
            EmptySpecialEffectRows(),
            refineryRows,
            now);

        Assert.Equal(0U, contribution.RelicAttributeHitpoints);
        Assert.Equal(0U, contribution.RefineryHitpoints);
        Assert.Equal(0U, contribution.Total);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateReportsMissingCatalogRowsForCandidateSecondaryEffects()
    {
        var playerItems = Snapshot(
            equipment:
            [
                Item(
                    itemId: 144_309,
                    position: 1,
                    durability: 1,
                    refineryEffectId: 724_440),
            ],
            mythsoulBag:
            [
                Item(itemId: 40_701_016, position: 217, durability: 1),
            ]);

        var contribution = LegacyItemSecondaryHitpointsCalculator.Calculate(playerItems);

        Assert.Equal(0U, contribution.Total);
        Assert.Contains("WorldDataCatalog.RefineryMaterials", contribution.MissingInputs);
        Assert.Contains("WorldDataCatalog.SpecialEffectAttributes", contribution.MissingInputs);
    }

    private static PlayerItemSnapshot Snapshot(
        IReadOnlyList<PlayerItemSnapshotItem>? equipment = null,
        IReadOnlyList<PlayerItemSnapshotItem>? mythsoulBag = null)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: mythsoulBag ?? [],
            EquipmentItems: equipment ?? [],
            RuneItems: []);
    }

    private static PlayerItemSnapshotItem Item(
        uint itemId,
        ushort position,
        ushort durability,
        uint attribute1 = 0,
        uint attribute2 = 0,
        uint attribute3 = 0,
        uint refineryEffectId = 0,
        uint refineryEffectDuration = 0,
        long refineryEffectAddedOn = 0)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Position = position,
            Durability = durability,
            MaximumDurability = durability,
            Attribute1 = attribute1,
            Attribute2 = attribute2,
            Attribute3 = attribute3,
            RefineryEffectId = refineryEffectId,
            RefineryEffectDuration = refineryEffectDuration,
            RefineryEffectAddedOn = refineryEffectAddedOn,
        };
    }

    private static uint EncodeRelicAttribute(LegacyChiAttributeType type, ushort value, bool epic = false)
    {
        return (uint)(value * 1000 + (byte)type + (epic ? 100 : 0));
    }

    private static IReadOnlyDictionary<int, SpecialEffectAttribute> EmptySpecialEffectRows()
    {
        return new Dictionary<int, SpecialEffectAttribute>();
    }

    private static IReadOnlyDictionary<int, RefineryMaterial> EmptyRefineryRows()
    {
        return new Dictionary<int, RefineryMaterial>();
    }

    private static SpecialEffectAttribute SpecialEffectAttribute(int itemId, int type, int value)
    {
        return new SpecialEffectAttribute(
            itemId,
            type,
            [value, 100, 0, 12_000, 72_000, 10],
            "speffect_attribute.txt",
            1,
            "",
            []);
    }

    private static RefineryMaterial RefineryMaterial(
        int itemType,
        int primaryValue,
        int effectType,
        int secondaryType = 0,
        int secondaryValue = 0)
    {
        return new RefineryMaterial(
            itemType,
            "Intensification",
            primaryValue,
            effectType,
            [primaryValue, effectType, secondaryType, secondaryValue],
            "Headgear",
            "Increases Max HP.",
            [],
            "Rifinery.txt",
            1,
            "",
            []);
    }
}
