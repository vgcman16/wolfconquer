using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using Table = WolfConquer.Core.Data.LegacyNativeInnerPowerTable;
using Attr = WolfConquer.Core.Data.LegacyNativeInnerPowerTable.AtributeType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInnerPowerTests
{
    [Theory]
    [MemberData(nameof(Attributes))]
    public void EveryAttributeUsesItsLiteralFieldAndUncheckedAddition(byte type)
    {
        using var scope = new Scope(); var inner = new Inner("Owner", 1);
        inner.AddAtributes((Attr)type, uint.MaxValue);
        var expected = new uint[14]; expected[type - 1] = uint.MaxValue;
        Assert.Equal(expected, Values(inner));
        inner.AddAtributes((Attr)type, 3); expected[type - 1] = 2;
        Assert.Equal(expected, Values(inner));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void UnknownAttributeDoesNotInventEffects(byte type)
    {
        using var scope = new Scope(); var inner = new Inner("Owner", 1);
        inner.AddAtributes((Attr)type, uint.MaxValue);
        Assert.All(Values(inner), value => Assert.Equal(0u, value));
    }

    [Fact]
    public void StatusResetsAllFieldsAndDividesEachValueBeforeScalingLevel()
    {
        using var scope = new Scope(Definition(1, 1));
        var row = Table.Stages[0].NeiGongAtributes[0];
        row.AtributesType = Enumerable.Range(1, 14).Select(value => (Attr)value).ToArray();
        row.AtributesValues = Enumerable.Repeat(8u, 14).ToArray(); row.MaxLevel = 3;
        var inner = new Inner("Owner", 1);
        foreach (var kind in row.AtributesType) inner.AddAtributes(kind, 100);
        inner.Stages[0].UnLocked = true;
        inner.Stages[0].NeiGongs[0].Unlocked = true; inner.Stages[0].NeiGongs[0].level = 2;
        inner.UpdateStatus(); Assert.All(Values(inner), value => Assert.Equal(4u, value));
        inner.Stages[0].UnLocked = false; inner.UpdateStatus();
        Assert.All(Values(inner), value => Assert.Equal(0u, value));
    }

    [Fact]
    public void LockedStageBreaksOuterLoopButCompletedFlagsStillGrantSpecialsAfterGongBreak()
    {
        using var scope = new Scope(Definition(1, 1, 2), Definition(2, 3), Definition(3, 4));
        foreach (var definition in Table.Stages)
        {
            definition.SpecialAtributesType = [Attr.MaxHP]; definition.AtributesValues = [20];
            foreach (var row in definition.NeiGongAtributes) { row.AtributesType = [Attr.PAttack]; row.AtributesValues = [90]; }
        }
        var inner = new Inner("Owner", 1);
        foreach (var stage in inner.Stages)
        {
            stage.UnLocked = true;
            foreach (var gong in stage.NeiGongs) { gong.Unlocked = true; gong.level = 3; gong.Complete = true; }
        }
        inner.Stages[0].NeiGongs[0].Unlocked = false;
        inner.Stages[1].UnLocked = false;
        inner.UpdateStatus(); Assert.Equal(20u, inner.MaxLife); Assert.Equal(0u, inner.AddAttack);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroLevelOrLockedGongBreaksRatherThanSkippingLaterGong(bool zeroLevel)
    {
        using var scope = new Scope(Definition(1, 1, 2, 3));
        foreach (var row in Table.Stages[0].NeiGongAtributes) { row.AtributesType = [Attr.PAttack]; row.AtributesValues = [9]; }
        var inner = new Inner("Owner", 1); inner.Stages[0].UnLocked = true;
        foreach (var gong in inner.Stages[0].NeiGongs) { gong.Unlocked = true; gong.level = 1; }
        if (zeroLevel) inner.Stages[0].NeiGongs[1].level = 0; else inner.Stages[0].NeiGongs[1].Unlocked = false;
        inner.UpdateStatus(); Assert.Equal(3u, inner.AddAttack);
    }

    [Fact]
    public void PartialMalformedRowLeavesEarlierMutationAndSkipsLaterRows()
    {
        using var scope = new Scope(Definition(1, 1));
        var row = Table.Stages[0].NeiGongAtributes[0];
        row.AtributesType = [Attr.MaxHP, Attr.PAttack]; row.AtributesValues = [9];
        var inner = new Inner("Owner", 1); inner.AddAtributes(Attr.PDefense, 100);
        inner.Stages[0].UnLocked = true; inner.Stages[0].NeiGongs[0].Unlocked = true; inner.Stages[0].NeiGongs[0].level = 2;
        Assert.Throws<IndexOutOfRangeException>(() => inner.UpdateStatus());
        Assert.Equal(6u, inner.MaxLife); Assert.Equal(0u, inner.Defence); Assert.Equal(0u, inner.AddAttack);
        row.MaxLevel = 0;
        Assert.Throws<DivideByZeroException>(() => inner.UpdateStatus());
        Assert.All(Values(inner), value => Assert.Equal(0u, value));
    }

    [Fact]
    public void EmptyGongsAreCompleteAndSourceAppliesSpecialWithoutAnyGong()
    {
        using var scope = new Scope(Definition());
        Table.Stages[0].SpecialAtributesType = [Attr.MaxHP]; Table.Stages[0].AtributesValues = [17];
        var inner = new Inner("Owner", 1); inner.Stages[0].UnLocked = true;
        inner.UpdateStatus(); Assert.Equal(17u, inner.MaxLife);
    }
}
