using WolfConquer.Core.Data;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed class LegacyTradeItemMetadataTests
{
    private const uint Type = 1_088_001;

    [Theory]
    [InlineData("0", 0, true)] [InlineData("0", 1, true)] [InlineData("0", 2, false)]
    [InlineData("1", 0, true)] [InlineData("1", 1, true)] [InlineData("1", 2, false)]
    [InlineData("10", 10, true)] [InlineData("10", 11, false)]
    [InlineData("65535", 65535, true)]
    [InlineData("", 1, false)] [InlineData("bad", 1, false)]
    [InlineData("-1", 1, false)] [InlineData("65536", 1, false)]
    public void ActualServerStackFormulaRequiresAValidCapturedCap(string cap, int stack, bool allowed)
    {
        var data = Fixture.Catalog(Row(cap));
        var metadata = Assert.IsType<LegacyTradeItemMetadata>(data.TradeItemMetadata);
        Assert.True(metadata.IsCurrent(data)); Assert.False(metadata.HasEmptySource);
        Assert.Equal(allowed, metadata.Allows(new() { ItemId = Type, StackSize = checked((ushort)stack) }));
        Assert.False(metadata.Allows(new() { ItemId = Type + 1, StackSize = 1 }));
    }

    [Theory]
    [InlineData("1", true)] [InlineData("2", false)] [InlineData("bad", false)]
    public void DuplicateSelectedRowsMustHaveOneUnambiguousEffectiveCap(string secondCap, bool allowed)
    {
        var data = Fixture.Catalog(Row("1"), Row(secondCap));
        Assert.Equal(allowed, data.TradeItemMetadata!.Allows(new() { ItemId = Type, StackSize = 1 }));
    }

    [Theory]
    [InlineData("short")] [InlineData("wrongId")] [InlineData("long")] [InlineData("zeroId")]
    public void MalformedRequiredServerRowCannotGrantEvenOneUnit(string malformed)
    {
        var valid = Row("1"); var fields = valid.Fields.ToArray();
        if (malformed == "short") fields = fields[..65];
        if (malformed == "long") fields = fields.Append("0").ToArray();
        if (malformed == "wrongId") fields[0] = "1088002";
        if (malformed == "zeroId") fields[0] = "0";
        var data = Fixture.Catalog(valid with { Fields = fields });
        Assert.False(data.TradeItemMetadata!.Allows(new() { ItemId = Type, StackSize = 1 }));
    }

    [Theory]
    [InlineData(350001U)] [InlineData(370001U)]
    public void GenuineAccessoryOverrideDoesNotConsumeItsUnusedCapColumn(uint type)
    {
        var row = Fixture.Item(type, fields: new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = "opaque" });
        Assert.True(row.IsLegacyAccessory);
        var data = Fixture.Catalog(row);
        Assert.True(data.TradeItemMetadata!.Allows(new() { ItemId = type, StackSize = 1 }));
        Assert.False(data.TradeItemMetadata.Allows(new() { ItemId = type, StackSize = 2 }));
    }

    [Fact]
    public void PublishedCapOwnsItsScalarsAndAValueEqualSuccessorIsADifferentGeneration()
    {
        var row = Row("1"); var data = Fixture.Catalog(row);
        var metadata = Assert.IsType<LegacyTradeItemMetadata>(data.TradeItemMetadata);
        Assert.IsType<string[]>(row.Fields)[ItemType.StackSizeFieldIndex] = "65535";
        Assert.True(metadata.Allows(new() { ItemId = Type, StackSize = 1 }));
        Assert.False(metadata.Allows(new() { ItemId = Type, StackSize = 2 }));
        var successor = Fixture.Catalog(Row("1"));
        Assert.False(metadata.IsCurrent(successor));
        Assert.NotSame(metadata, successor.TradeItemMetadata);
    }

    [Fact]
    public void InvalidUnrelatedGroupDoesNotPoisonAValidSelectedTypeOrBecomeAnEmptyCompatibilityOwner()
    {
        var data = Fixture.Catalog(Row("10"), Fixture.Item(1000000,
            fields: new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = "bad" }));
        Assert.True(data.TradeItemMetadata!.Allows(new() { ItemId = Type, StackSize = 10 }));
        Assert.False(data.TradeItemMetadata.HasEmptySource);
        Assert.False(data.TradeItemMetadata.Allows(new() { ItemId = 1000000, StackSize = 1 }));
        Assert.True(Fixture.Catalog().TradeItemMetadata!.HasEmptySource);
    }

    private static ItemType Row(string cap) => Fixture.Item(Type,
        fields: new Dictionary<int, string> { [ItemType.StackSizeFieldIndex] = cap });
}
