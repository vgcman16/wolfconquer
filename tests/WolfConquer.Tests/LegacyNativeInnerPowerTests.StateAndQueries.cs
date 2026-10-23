using WolfConquer.Core.World;
using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using Table = WolfConquer.Core.Data.LegacyNativeInnerPowerTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInnerPowerTests
{
    [Fact]
    public void BirthUsesDefinitionsInArrayOrderAndOnlyPublishesAfterSuccess()
    {
        using var scope = new Scope(Definition(2, 8, 2), Definition(1, 9));
        var inner = new Inner("Owner", 7);
        Assert.Equal("Owner", inner.Name); Assert.Equal(7u, inner.UID); Assert.Equal(0, inner.Potency);
        Assert.Equal(new ushort[] { 2, 1 }, inner.Stages.Select(stage => stage.ID));
        Assert.Equal(new byte[] { 8, 2 }, inner.Stages[0].NeiGongs.Select(gong => gong.ID));
        Assert.All(inner.Stages, stage => Assert.False(stage.UnLocked));
        Assert.All(inner.Stages.SelectMany(stage => stage.NeiGongs), gong =>
        { Assert.False(gong.Unlocked); Assert.False(gong.Complete); Assert.Equal(0, gong.level); Assert.Equal(0, gong.Score); });
        Assert.Same(inner, Inner.InnerPowerPolle[7]); Assert.All(Values(inner), value => Assert.Equal(0u, value));
        Table.Stages[1] = null!;
        Assert.Throws<NullReferenceException>(() => new Inner("Broken", 9));
        Assert.False(Inner.InnerPowerPolle.ContainsKey(9)); Assert.Same(inner, Inner.InnerPowerPolle[7]);
    }

    [Fact]
    public void DuplicateUidCreatesIndependentObjectWithoutReplacingPoolOrAssigningPlayer()
    {
        using var scope = new Scope(Definition(1, 1));
        var first = new Inner("First", 4); var second = new Inner("Second", 4);
        Assert.NotSame(first, second); Assert.Same(first, Inner.InnerPowerPolle[4]);
        Assert.Null(new LegacyNativeGameClientRuntime().Player.InnerPower);
        first.Stages[0].NeiGongs[0].Score = 3;
        Assert.Equal(0u, second.TotalScore);
    }

    [Fact]
    public void EmptyCountBirthDoesNotDereferenceDefinitionArrayAndStageDefaultsRemainNull()
    {
        using var scope = new Scope(); Table.Stages = null!;
        var inner = new Inner(null!, 4);
        Assert.Null(inner.Name); Assert.Empty(inner.Stages); Assert.Equal(0u, inner.TotalScore);
        Assert.Null(new Inner.Stage().NeiGongs);
        Assert.Throws<NullReferenceException>(() => new Inner.Stage().Complete);
        Table.Count = 1;
        Assert.Throws<NullReferenceException>(() => new Inner("Broken", 5));
        Assert.False(Inner.InnerPowerPolle.ContainsKey(5));
    }

    [Fact]
    public void QueriesRetainPhysicalOrderDuplicatesAndDifferentStageFiltering()
    {
        using var scope = new Scope(Definition(1, 2, 1), Definition(2, 2), Definition(3, 4));
        var inner = new Inner("Owner", 1);
        inner.Stages[0].NeiGongs[0].Unlocked = true;
        inner.Stages[1].UnLocked = true; inner.Stages[1].NeiGongs[0].Unlocked = true;
        inner.Stages[2].UnLocked = true; inner.Stages[2].NeiGongs[0].Unlocked = true;
        Assert.True(inner.TryGetStageAndGong(2, out var stage, out var gong));
        Assert.Same(inner.Stages[0], stage); Assert.Same(stage!.NeiGongs[0], gong);
        Assert.True(inner.isUnlockedNeiGong(3)); // Searches ID2 even though its stage is locked.
        Assert.True(inner.isUnlockedNeiGong(1)); Assert.False(inner.isUnlockedNeiGong(0));
        Assert.False(inner.isUnlockedNeiGong(6));
        Assert.Equal(new[] { inner.Stages[1].NeiGongs[0], inner.Stages[2].NeiGongs[0] }, inner.GetNeiGongs());
        Assert.True(inner.GetLastStage(out stage)); Assert.Same(inner.Stages[2], stage);
        Assert.False(inner.TryGetStageAndGong(99, out stage, out gong)); Assert.Null(stage); Assert.Null(gong);
        inner.Stages[1].UnLocked = false; inner.Stages[2].UnLocked = false;
        Assert.False(inner.GetLastStage(out stage)); Assert.Null(stage);
    }

    [Fact]
    public void StageCompletionScoresAndLastUnlockedUseEveryEntry()
    {
        using var scope = new Scope(Definition(1, 9, 1));
        var stage = new Inner("Owner", 1).Stages[0];
        Assert.False(stage.Complete); Assert.False(stage.GetLastNeiGang(out var gong)); Assert.Null(gong);
        foreach (var entry in stage.NeiGongs) { entry.Score = 255; entry.Complete = true; entry.Unlocked = true; }
        Assert.Equal(510u, stage.Score); Assert.True(stage.Complete);
        Assert.True(stage.GetLastNeiGang(out gong)); Assert.Same(stage.NeiGongs[1], gong);
        stage.NeiGongs = []; Assert.True(stage.Complete); Assert.Equal(0u, stage.Score);
    }

    [Fact]
    public void AttributeCountsUsePassedLengthNotPassedGongIdentityAndNarrowToUInt16()
    {
        using var scope = new Scope(Definition(1, 1, 2));
        Table.Stages[0].NeiGongAtributes[0].AtributesValues = new uint[65535];
        Table.Stages[0].NeiGongAtributes[1].AtributesValues = new uint[2];
        var stage = new Inner("Owner", 1).Stages[0];
        Assert.Equal((ushort)1, stage.GetNoumberAtributes([null!, null!]));
        Assert.Equal(ushort.MaxValue, stage.GetAtributesCount(0)); Assert.Equal((ushort)2, stage.GetAtributesCount(1));
        Assert.Throws<IndexOutOfRangeException>(() => stage.GetNoumberAtributes([null!, null!, null!]));
        Table.Stages[0].NeiGongAtributes[0].AtributesValues = new uint[65536];
        Assert.Equal((ushort)0, stage.GetAtributesCount(0));
    }

    [Theory]
    [InlineData(2130u, 2u, 130u, true)]
    [InlineData(2130u, 1u, 255u, false)]
    [InlineData(2130u, 2u, 129u, false)]
    [InlineData(2999u, 2u, 231u, true)]
    [InlineData(256000u, 0u, 0u, true)]
    public void AccountRequirementNarrowsBothDecodedComponents(uint requirement, uint reborn, uint level, bool expected)
    {
        var gong = new Table.Stage.NeiGong { ReqLev = requirement };
        Assert.Equal(expected, gong.CheckAccount(reborn, level));
    }

    [Fact]
    public void DatabaseLookupReturnsFirstPhysicalMatchAndFaultsRatherThanSkippingNullRows()
    {
        using var scope = new Scope(Definition(2, 8), Definition(1, 8));
        Assert.True(Table.GetDBInfo(8, out var stage, out var gong));
        Assert.Same(Table.Stages[0], stage); Assert.Same(stage!.NeiGongAtributes[0], gong);
        Assert.False(Table.GetDBInfo(300, out stage, out gong)); Assert.Null(stage); Assert.Null(gong);
        Table.Stages[0] = null!;
        Assert.Throws<NullReferenceException>(() => Table.GetDBInfo(8, out _, out _));
    }
}
