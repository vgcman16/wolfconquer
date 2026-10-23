using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyExchangeMaterialTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task DuplicateMaterialDefinitionsRequireTheirSumBeforeAnyPurchaseEffect(ushort available)
    {
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, 2), (ExchangeMaterialA, 3)]),
            [Stock(101, ExchangeMaterialA, available)]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy();
        fixture.AssertRejected(character, items);
        Assert.Contains("materialAggregateShortage", fixture.Log.ToString());
        Assert.Equal(available, fixture.Original.PlayerItems.InventoryItems[0].StackSize);
    }

    [Fact]
    public async Task FourRepeatedDefinitionsCannotReuseEachOthersIndividuallySufficientStock()
    {
        using var fixture = new Fixture(ExchangeRow(materials:
            [(ExchangeMaterialA, 1), (ExchangeMaterialA, 2), (ExchangeMaterialA, 3), (ExchangeMaterialA, 4)]),
            [Stock(101, ExchangeMaterialA, 8)]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy(count: 2);
        fixture.AssertRejected(character, items); Assert.Contains("materialAggregateShortage", fixture.Log.ToString());
    }

    [Fact]
    public async Task LateDifferentMaterialShortageCannotLeakTheEarlierMaterialDebit()
    {
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, 1), (ExchangeMaterialB, 2)]),
            [Stock(101, ExchangeMaterialA, 1), Stock(201, ExchangeMaterialB, 1)]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy();
        fixture.AssertRejected(character, items); Assert.Contains("materialAggregateShortage", fixture.Log.ToString());
    }

    [Theory]
    [InlineData(uint.MaxValue)]
    [InlineData(2_147_483_648u)]
    public async Task CheckedMaterialProductOverflowRefusesInsteadOfWrappingOrThrowing(uint amount)
    {
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, 1), (ExchangeMaterialB, amount)]),
            [Stock(101, ExchangeMaterialA, 2), Stock(201, ExchangeMaterialB, ushort.MaxValue)]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy(count: 2);
        fixture.AssertRejected(character, items); Assert.Contains("materialRequirementOverflow", fixture.Log.ToString());
    }

    [Theory]
    [InlineData(uint.MaxValue, 1u)]
    [InlineData(4_294_967_294u, 2u)]
    [InlineData(2_147_483_648u, 2_147_483_648u)]
    public async Task CheckedRepeatedMaterialSumOverflowRefusesBeforeAnyDebit(uint first, uint second)
    {
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, first), (ExchangeMaterialA, second)]),
            [Stock(101, ExchangeMaterialA, ushort.MaxValue)]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy();
        fixture.AssertRejected(character, items); Assert.Contains("materialRequirementOverflow", fixture.Log.ToString());
    }

    [Fact]
    public async Task ExactUInt32RequirementBoundaryIsAStockShortageNotAnArithmeticWrap()
    {
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, uint.MaxValue)]),
            [Stock(101, ExchangeMaterialA, ushort.MaxValue)]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy();
        fixture.AssertRejected(character, items); Assert.Contains("materialAggregateShortage", fixture.Log.ToString());
        Assert.DoesNotContain("materialRequirementOverflow", fixture.Log.ToString());
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(65_536u)]
    [InlineData(uint.MaxValue)]
    public async Task RequestCountLimitsStillRejectBeforeMaterialOrRewardEffects(uint count)
    {
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, 1)]), [Stock(101, ExchangeMaterialA, 5)]);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy(count: count);
        fixture.AssertRejected(character, items); Assert.Contains("outside legacy bounds", fixture.Log.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ExistingShopMetadataAndAmountGuardsRemainBeforeDebits(int failure)
    {
        using var fixture = new Fixture(ExchangeRow(amount: failure == 2 ? 1u : 0u, materials: [(ExchangeMaterialA, 1)]),
            [Stock(101, ExchangeMaterialA, 5)], includeReward: failure != 3, purchased: failure == 2 ? 1u : null);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy(shop: failure == 0 ? 24u : 23u, index: failure == 1 ? 445u : 444u);
        fixture.AssertRejected(character, items);
    }

    [Fact]
    public async Task FullInventoryRuleIsNotRetunedToGrantSpaceAfterMaterialConsumption()
    {
        var inventory = Enumerable.Range(0, 40).Select(index =>
            Stock((uint)(100 + index), index == 0 ? ExchangeMaterialA : 799999u, 5)).ToArray();
        using var fixture = new Fixture(ExchangeRow(materials: [(ExchangeMaterialA, 5)]), inventory);
        byte[] character = File.ReadAllBytes(fixture.CharacterFile), items = File.ReadAllBytes(fixture.ItemFile);
        await fixture.Buy(); fixture.AssertRejected(character, items);
        Assert.Contains("inventory is full", fixture.Log.ToString());
    }
}
