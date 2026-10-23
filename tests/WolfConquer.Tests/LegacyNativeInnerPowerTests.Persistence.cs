using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using Table = WolfConquer.Core.Data.LegacyNativeInnerPowerTable;
using Attr = WolfConquer.Core.Data.LegacyNativeInnerPowerTable.AtributeType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInnerPowerTests
{
    [Fact]
    public void RecordPreservesAllGongsInUnlockedStagesAndTrailingDelimiter()
    {
        using var scope = new Scope(Definition(1, 1, 2), Definition(2, 3));
        var inner = new Inner("Wolf", 7) { Potency = -2 };
        inner.Stages[0].UnLocked = true;
        var gong = inner.Stages[0].NeiGongs[0];
        gong.Score = 255; gong.Unlocked = true; gong.level = 3; gong.Complete = true;
        inner.Stages[1].NeiGongs[0].Score = 200;
        Assert.Equal("Wolf/7/-2/1/1/1/2/1/255/1/3/1/2/0/0/0/0/", inner.ToString());
        inner.Stages[0].UnLocked = false;
        Assert.Equal("Wolf/7/-2/0/", inner.ToString());
    }

    [Fact]
    public void SaveAndLoadUseActualCountRecordFileAndDoNotRecalculateAttributes()
    {
        using var scope = new Scope(Definition(1, 1));
        var row = Table.Stages[0].NeiGongAtributes[0]; row.AtributesType = [Attr.MaxHP]; row.AtributesValues = [9];
        var inner = new Inner("Wolf", 7) { Potency = 90 };
        inner.Stages[0].UnLocked = true;
        var gong = inner.Stages[0].NeiGongs[0]; gong.Unlocked = true; gong.Score = 80; gong.level = 2;
        inner.UpdateStatus(); Assert.Equal(6u, inner.MaxLife);
        File.WriteAllText(scope.FilePath, "old file must be replaced");
        Table.Save();
        Assert.Equal(new[] { "Count=1", "Wolf/7/90/1/1/1/1/1/80/1/2/0/" }, File.ReadAllLines(scope.FilePath));
        Inner.InnerPowerPolle.Clear();
        Table.Load();
        var loaded = Assert.Single(Inner.InnerPowerPolle).Value;
        Assert.NotSame(inner, loaded); Assert.Equal(inner.ToString(), loaded.ToString());
        Assert.Equal(80u, loaded.TotalScore); Assert.Equal(0u, loaded.MaxLife);
        Assert.Same(loaded, Assert.Single(Inner.InnerPowerRank.GetRankingList()));
        loaded.UpdateStatus(); Assert.Equal(6u, loaded.MaxLife);
    }

    [Fact]
    public void SparseUnlockedStageRecordsLoadIntoSequentialSlotsNotByStageId()
    {
        using var scope = new Scope(Definition(1, 1), Definition(2, 2));
        var inner = new Inner("Sparse", 5);
        inner.Stages[1].UnLocked = true; inner.Stages[1].NeiGongs[0].ID = 8;
        inner.Stages[1].NeiGongs[0].Score = 11;
        Table.Save(); Inner.InnerPowerPolle.Clear(); Table.Load();
        var loaded = Inner.InnerPowerPolle[5];
        Assert.Equal((ushort)2, loaded.Stages[0].ID); Assert.True(loaded.Stages[0].UnLocked);
        Assert.Equal((byte)8, loaded.Stages[0].NeiGongs[0].ID); Assert.Equal(11u, loaded.TotalScore);
        Assert.False(loaded.Stages[1].UnLocked); Assert.Equal((ushort)2, loaded.Stages[1].ID);
    }

    [Fact]
    public void DuplicateRecordUidDoesNotReplacePoolAndRankingRetainsItsFirstObject()
    {
        using var scope = new Scope(Definition(1, 1));
        File.WriteAllText(scope.FilePath, "Count=2\nFirst/5/1/1/1/1/1/1/4/1/1/0/\nSecond/5/2/1/1/1/1/1/9/1/1/0/\n");
        Table.Load();
        Assert.Equal("First", Inner.InnerPowerPolle[5].Name);
        Assert.Equal(4u, Inner.InnerPowerPolle[5].TotalScore);
        Assert.Same(Inner.InnerPowerPolle[5], Assert.Single(Inner.InnerPowerRank.GetRankingList()));
    }

    [Fact]
    public void MalformedRecordPublishesConstructorAndEarlierGongBeforeLaterArrayFault()
    {
        using var scope = new Scope(Definition(1, 1));
        scope.Record("Fault/5/33/1/1/1/2/9/8/1/2/1/10/7/1/1/0/");
        Assert.Throws<IndexOutOfRangeException>(() => Table.Load());
        var loaded = Inner.InnerPowerPolle[5];
        Assert.Equal(33, loaded.Potency); Assert.True(loaded.Stages[0].UnLocked);
        Assert.Equal((byte)9, loaded.Stages[0].NeiGongs[0].ID);
        Assert.Equal((byte)8, loaded.Stages[0].NeiGongs[0].Score);
        Assert.True(loaded.Stages[0].NeiGongs[0].Complete);
        Assert.Throws<ArgumentNullException>(() => Inner.InnerPowerRank.GetRankingList());
    }

    [Fact]
    public void MalformedTokensUseNativeDefaultsButOnlyLiteralOneMeansTrue()
    {
        using var scope = new Scope(Definition(1, 1));
        scope.Record("Defaults/5/bad/1/bad/2/1/bad/999/2/bad/2/");
        Table.Load(); var inner = Inner.InnerPowerPolle[5];
        Assert.Equal(0, inner.Potency); Assert.Equal((ushort)0, inner.Stages[0].ID);
        Assert.False(inner.Stages[0].UnLocked);
        var gong = inner.Stages[0].NeiGongs[0];
        Assert.Equal((byte)0, gong.ID); Assert.Equal((byte)0, gong.Score);
        Assert.False(gong.Unlocked); Assert.False(gong.Complete); Assert.Equal((byte)0, gong.level);
        Assert.Empty(Inner.InnerPowerRank.GetRankingList());
    }

    [Fact]
    public void MissingAndZeroCountFilesPreserveExistingPoolAndSaveEmptyWritesCountZero()
    {
        using var scope = new Scope(); var existing = new Inner("Existing", 5);
        Table.Load(); Assert.Same(existing, Inner.InnerPowerPolle[5]);
        File.WriteAllText(scope.FilePath, "Count=0\n"); Table.Load(); Assert.Same(existing, Inner.InnerPowerPolle[5]);
        Inner.InnerPowerPolle.Clear(); Table.Save();
        Assert.Equal(new[] { "Count=0" }, File.ReadAllLines(scope.FilePath));
    }
}
