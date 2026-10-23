using WolfConquer.Core.Data;
using RuneTable = WolfConquer.Core.Data.LegacyNativeRuneTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStatTests
{
    [Theory]
    [InlineData(730000u, 0u)] [InlineData(730001u, 10u)] [InlineData(730002u, 40u)]
    [InlineData(730003u, 120u)] [InlineData(730004u, 360u)] [InlineData(730005u, 1080u)]
    [InlineData(730006u, 3240u)] [InlineData(730007u, 9720u)] [InlineData(730008u, 29160u)]
    [InlineData(730009u, 0u)] [InlineData(999999u, 0u)]
    public void RawPlusPointsRetainsExactNativePlusStoneFallbackRange(uint id, uint expected)
    {
        using var scope = new Scope();
        Assert.Equal(expected, RuneTable.GetRunePlusPoints(id));
    }

    [Fact]
    public void RawPlusPointsUsesFirstMatchingTypeInPhysicalEnumerationAndIgnoresLevel()
    {
        using var scope = new Scope();
        RuneTable.RuneLevelEXPList[999] = Exp(403, 9, 37);
        RuneTable.RuneLevelEXPList[1] = Exp(403, 1, 999);
        Assert.Equal(37u, RuneTable.GetRunePlusPoints(4030001));
        Assert.Equal(37u, RuneTable.GetRunePlusPoints(4030099));
        RuneTable.RuneLevelEXPList[77] = Exp(730, 1, 7);
        Assert.Equal(7u, RuneTable.GetRunePlusPoints(730008)); //Native row overrides stone fallback.
        RuneTable.RuneLevelEXPList[999].RequiredProgress = 41;
        Assert.Equal(41u, RuneTable.GetRunePlusPoints(4030001));
    }

    [Fact]
    public void ItemPlusPointsUsesLevelStackAndProgressButDoesNotUseRawOverloadStoneFallback()
    {
        using var scope = new Scope();
        RuneTable.RuneLevelEXPList[50] = Exp(403, 1, 10);
        RuneTable.RuneLevelEXPList[2] = Exp(403, 9, 37);
        var item = Item(1, 4030009); item.StackSize = 3; item.RuneEXP = 11;
        Assert.Equal(122u, RuneTable.GetRunePlusPoints(item));
        item.StackSize = 0;
        Assert.Equal(48u, RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)1, item.StackSize);
        var stone = Item(2, 730008); stone.RuneEXP = 99;
        Assert.Equal(0u, RuneTable.GetRunePlusPoints(stone));
        Assert.Equal((ushort)1, stone.StackSize);
    }

    [Fact]
    public void ItemPlusPointsMutatesStackBeforeMissingRowOrOwnerFaultButAfterPrefixConversion()
    {
        using var scope = new Scope();
        var item = Item(1, 4030001); item.RuneEXP = 999;
        Assert.Equal(0u, RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)1, item.StackSize);
        item.StackSize = 0; RuneTable.RuneLevelEXPList = null!;
        Assert.Throws<NullReferenceException>(() => RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)1, item.StackSize);
        var shortId = Item(2, 99999);
        Assert.Throws<ArgumentOutOfRangeException>(() => RuneTable.GetRunePlusPoints(shortId));
        Assert.Equal((ushort)0, shortId.StackSize);
        Assert.Throws<NullReferenceException>(() => RuneTable.GetRunePlusPoints(null!));
    }

    [Theory]
    [InlineData(0u)] [InlineData(999u)] [InlineData(1000u)] [InlineData(99999u)]
    public void RawPlusPointsRetainsSubstringFaultForShortTypePrefixBeforeAnyTableRead(uint value)
    {
        using var scope = new Scope();
        RuneTable.RuneLevelEXPList = null!;
        Assert.Throws<ArgumentOutOfRangeException>(() => RuneTable.GetRunePlusPoints(value));
    }

    [Fact]
    public void ItemPlusPointsRetainsUInt32ProductAndAdditionWrapAndFirstDuplicateTypeLevel()
    {
        using var scope = new Scope();
        RuneTable.RuneLevelEXPList[10] = Exp(403, 9, uint.MaxValue);
        RuneTable.RuneLevelEXPList[1] = Exp(403, 9, 999);
        var item = Item(1, 4030009); item.StackSize = 2; item.RuneEXP = 3;
        Assert.Equal(1u, RuneTable.GetRunePlusPoints(item));
        item.StackSize = ushort.MaxValue; item.RuneEXP = uint.MaxValue;
        Assert.Equal(unchecked(uint.MaxValue * (uint)ushort.MaxValue + uint.MaxValue), RuneTable.GetRunePlusPoints(item));
    }

    [Fact]
    public void PlusPointsDoesNotSkipNullRowsEvenWhenLaterTypeWouldMatch()
    {
        using var scope = new Scope();
        RuneTable.RuneLevelEXPList[99] = null!;
        RuneTable.RuneLevelEXPList[1] = Exp(403, 9, 3);
        Assert.Throws<NullReferenceException>(() => RuneTable.GetRunePlusPoints(4030009));
        var item = Item(1, 4030009);
        Assert.Throws<NullReferenceException>(() => RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)1, item.StackSize);
    }

    [Fact]
    public void RawPlusPointsUsesFirstThreeDecimalDigitsRatherThanWholeQuotient()
    {
        using var scope = new Scope();
        RuneTable.RuneLevelEXPList[1] = Exp(429, 0, 73);
        Assert.Equal(73u, RuneTable.GetRunePlusPoints(uint.MaxValue));
        RuneTable.RuneLevelEXPList[1].ItemType = 4294967;
        Assert.Equal(0u, RuneTable.GetRunePlusPoints(uint.MaxValue));
    }
}
