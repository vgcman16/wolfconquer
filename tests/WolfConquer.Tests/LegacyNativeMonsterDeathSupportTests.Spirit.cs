using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterDeathSupportTests
{
    [Fact]
    public void SpiritRecordsRetainNativeDefaultsAndUndefinedEnumValues()
    {
        var row = new LegacyNativeSpiritTable.Spirit();
        Assert.Equal(0u, row.Index); Assert.Equal(0u, row.AnimaID); Assert.Equal(LegacyNativeSpiritTable.Type.Failed, row.Type);
        Assert.False(row.IsItem); Assert.Equal(0u, row.NewAnimaID);
        Assert.Equal((byte)0, (byte)LegacyNativeSpiritTable.Type.Failed);
        Assert.Equal((byte)1, (byte)LegacyNativeSpiritTable.Type.Upgraded);
        Assert.Equal((byte)2, (byte)LegacyNativeSpiritTable.Type.Awkaned);
        row.Type = (LegacyNativeSpiritTable.Type)255; Assert.Equal((byte)255, (byte)row.Type);
    }
    [Fact]
    public void MissingFileDoesNotClearReplaceOrAllocateTheList()
    {
        using var f = new Files(); var prior = LegacyNativeSpiritTable.SpiritRates;
        var row = new LegacyNativeSpiritTable.Spirit { Index = 9 }; prior.Add(row); LegacyNativeSpiritTable.Load();
        Assert.Same(prior, LegacyNativeSpiritTable.SpiritRates); Assert.Same(row, Assert.Single(prior));
        LegacyNativeSpiritTable.SpiritRates = null!; LegacyNativeSpiritTable.Load(); Assert.Null(LegacyNativeSpiritTable.SpiritRates);
    }
    [Fact]
    public void FullLoadAppendsExactParsedRowsWithoutReplacingOrDeduplicating()
    {
        using var f = new Files(); var prior = LegacyNativeSpiritTable.SpiritRates;
        var old = new LegacyNativeSpiritTable.Spirit { Index = 9 }; prior.Add(old);
        f.Write("1@@ignored@@3007110@@1@@1@@3007111", "1@@again@@3007110@@2@@0@@3007112", "2@@ignored@@3007112@@255@@2@@0");
        LegacyNativeSpiritTable.Load();
        Assert.Same(prior, LegacyNativeSpiritTable.SpiritRates); Assert.Equal(4, prior.Count); Assert.Same(old, prior[0]);
        Assert.Equal(1u, prior[1].Index); Assert.Equal(3007110u, prior[1].AnimaID); Assert.Equal(LegacyNativeSpiritTable.Type.Upgraded, prior[1].Type);
        Assert.True(prior[1].IsItem); Assert.Equal(3007111u, prior[1].NewAnimaID);
        Assert.Equal(1u, prior[2].Index); Assert.Equal(LegacyNativeSpiritTable.Type.Awkaned, prior[2].Type); Assert.False(prior[2].IsItem);
        Assert.Equal((LegacyNativeSpiritTable.Type)255, prior[3].Type); Assert.False(prior[3].IsItem);
        LegacyNativeSpiritTable.Load(); Assert.Equal(7, prior.Count); Assert.NotSame(prior[1], prior[4]);
    }
    [Theory]
    [InlineData("bad@@x@@10@@1@@1@@11")]
    [InlineData("1@@x@@4294967296@@1@@1@@11")]
    [InlineData("1@@x@@10@@256@@1@@11")]
    [InlineData("1@@x@@10@@1@@256@@11")]
    [InlineData("1@@x@@10@@1@@1@@bad")]
    [InlineData("1@@x@@10@@1")]
    [InlineData(" ")]
    [InlineData("@@@@")]
    public void BadRowsAreCaughtAndLaterValidRowsStillAppend(string bad)
    {
        using var f = new Files(); f.Write("1@@x@@10@@0@@0@@9", bad, "2@@x@@11@@1@@1@@12");
        LegacyNativeSpiritTable.Load(); var rows = LegacyNativeSpiritTable.SpiritRates;
        Assert.Equal(2, rows.Count); Assert.Equal(1u, rows[0].Index); Assert.Equal(2u, rows[1].Index); Assert.Equal(12u, rows[1].NewAnimaID);
    }
    [Fact]
    public void EmptyLinesSkipWhileRemovedEmptyCellsKeepTheNativeShiftedColumnPositions()
    {
        using var f = new Files();
        f.Write("", "1@@@@2@@10@@1@@1@@11", "", "2@@unused@@12@@0@@0@@13");
        LegacyNativeSpiritTable.Load(); var rows = LegacyNativeSpiritTable.SpiritRates;
        Assert.Equal(2, rows.Count); Assert.Equal(10u, rows[0].AnimaID); Assert.Equal(11u, rows[0].NewAnimaID);
        Assert.Equal(12u, rows[1].AnimaID);
    }
    [Fact]
    public void NullListAppendFaultIsCaughtPerRowWithoutSynthesizingANewOwner()
    {
        using var f = new Files(); f.Write("1@@x@@10@@1@@1@@11", "2@@x@@11@@0@@0@@10");
        LegacyNativeSpiritTable.SpiritRates = null!; LegacyNativeSpiritTable.Load(); Assert.Null(LegacyNativeSpiritTable.SpiritRates);
    }
    [Fact]
    public void SourceFileReadFaultOccursOutsideThePerRowCatch()
    {
        using var f = new Files(); f.Write("1@@x@@10@@1@@1@@11");
        using var held = new FileStream(f.Pathname, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(LegacyNativeSpiritTable.Load); Assert.Empty(LegacyNativeSpiritTable.SpiritRates);
    }
}
