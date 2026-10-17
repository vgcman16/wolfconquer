using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMaxRandomRelicPolicyTests
{
    [Theory]
    [InlineData(0, 4_100_001U)]
    [InlineData(1, 4_100_002U)]
    [InlineData(2, 4_100_003U)]
    [InlineData(3, 4_100_004U)]
    [InlineData(4, 4_100_005U)]
    public void MaxGeneratorCreatesFiveUniqueEpicMaximumAttributes(
        int itemIndex,
        uint expectedItemId)
    {
        var result = LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(
            LegacyMaxRandomRelicTestData.CreateCatalog(),
            new LegacyBossSpecialDeathDropRolls(
                DeityLandRelicItemIndex: itemIndex,
                DeityLandRelicAttributeIndexes: [0, 0, 0, 0, 0]),
            out var generated,
            out var reason);

        Assert.True(result, reason);
        Assert.Equal(expectedItemId, generated.ItemId);
        Assert.Equal((ushort)10_099, generated.Durability);
        Assert.Equal((ushort)10_099, generated.MaximumDurability);
        Assert.Equal(
            LegacyMaxRandomRelicTestData.ExpectedEncodedAttributes,
            new[]
            {
                generated.Attribute1,
                generated.Attribute2,
                generated.Attribute3,
                generated.Attribute4,
                generated.Attribute5,
            });
        Assert.Contains("Max:true", generated.Source, StringComparison.Ordinal);
        Assert.Contains("attributeCount=5", generated.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void MaxGeneratorFailsWhenSelectedRelicHasNoRandomAttributeRows()
    {
        var result = LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(
            LegacyMaxRandomRelicTestData.CreateCatalog(includeRandomAttributes: false),
            new LegacyBossSpecialDeathDropRolls(DeityLandRelicItemIndex: 0),
            out _,
            out var reason);

        Assert.False(result);
        Assert.Contains("no matching rows", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void MaxGeneratorRejectsNonEpicRows()
    {
        var result = LegacyBossSpecialDeathDropPolicy.TryCreateMaxRandomRelicItem(
            LegacyMaxRandomRelicTestData.CreateCatalog(epic: false),
            new LegacyBossSpecialDeathDropRolls(DeityLandRelicItemIndex: 0),
            out _,
            out var reason);

        Assert.False(result);
        Assert.Contains("five unique epic", reason, StringComparison.Ordinal);
    }
}
