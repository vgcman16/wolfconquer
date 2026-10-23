using RuneTable = WolfConquer.Core.Data.LegacyNativeRuneTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStatTests
{
    [Fact]
    public void AnyRemainderPlusPointsRejectsInvalidCatalogTailEvenAfterMatchingRow()
    {
        using var scope = new Scope();
        var match = Exp(403, 9, 7);
        RuneTable.RuneLevelEXPList.Add(99, match);
        RuneTable.RuneLevelEXPList.Add(1, null!);
        var item = Item(17, 4030009); item.StackSize = 0; item.RuneEXP = 5;
        Assert.Equal(0u, RuneTable.GetRunePlusPoints(4030009));
        Assert.Equal(0u, RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)0, item.StackSize); Assert.Equal(5u, item.RuneEXP);
        Assert.Same(match, RuneTable.RuneLevelEXPList[99]); Assert.Null(RuneTable.RuneLevelEXPList[1]);
    }

    [Fact]
    public void AnyRemainderPlusPointsAllowsExactUInt32MaximumButRejectsTheNextQuote()
    {
        using var scope = new Scope();
        RuneTable.RuneLevelEXPList[99] = Exp(403, 9, uint.MaxValue);
        var item = Item(17, 4030009); item.StackSize = 0;
        Assert.Equal(uint.MaxValue, RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)0, item.StackSize);
        item.RuneEXP = 1;
        Assert.Equal(0u, RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)0, item.StackSize); Assert.Equal(1u, item.RuneEXP);
    }

    [Fact]
    public void AnyRemainderPlusPointsUsesFirstPhysicalMatchNotDictionaryKeysOrSummedAliases()
    {
        using var scope = new Scope();
        var match = Exp(403, 9, 7);
        RuneTable.RuneLevelEXPList.Add(999, match);
        RuneTable.RuneLevelEXPList.Add(1, match);
        RuneTable.RuneLevelEXPList.Add(403, Exp(999, 9, 10000));
        var item = Item(17, 4030009); item.StackSize = 2; item.RuneEXP = 5;
        Assert.Equal(7u, RuneTable.GetRunePlusPoints(4030009));
        Assert.Equal(19u, RuneTable.GetRunePlusPoints(item));
        match.RequiredProgress = 8;
        Assert.Equal(21u, RuneTable.GetRunePlusPoints(item));
        Assert.Equal((ushort)2, item.StackSize); Assert.Equal(5u, item.RuneEXP);
    }
}
