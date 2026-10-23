using WolfConquer.Core.World;
using Base = WolfConquer.Core.World.LegacyNativeAttackBase;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Theory]
    [InlineData(3, 1)] [InlineData(0, 2)] [InlineData(-1, 3)] [InlineData(-5, 3)] [InlineData(-6, 4)]
    public void BaseNameTypeKeepsAllNativeRelativeLevelBands(int delta, int expected) => Assert.Equal(expected, Base.GetNameType(100 + delta, 100));

    [Theory]
    [InlineData(0, false, 110)] [InlineData(2, false, 110)] [InlineData(3, false, 140)]
    [InlineData(5, false, 140)] [InlineData(6, false, 190)] [InlineData(10, false, 190)]
    [InlineData(11, false, 240)] [InlineData(20, false, 240)] [InlineData(21, false, 290)]
    [InlineData(-1, false, 90)] [InlineData(20, true, 90)]
    public void BaseMonsterDamageRetainsEveryLevelMultiplierAndRange117Clamp(int difference, bool ranged, int expected) =>
        Assert.Equal(expected, Base.CalcDamageUser2Monster(100, 10, 120 + difference, 120, ranged));

    [Fact]
    public void BaseAdjustmentConstantsBranchesAndOriginalShadowedFullSentinelAreNotNormalized()
    {
        Assert.Equal(-30000, Base.StatusConstants.AdjustSet); Assert.Equal(-32768, Base.StatusConstants.AdjustFull); Assert.Equal(30000, Base.StatusConstants.AdjustPercent);
        Assert.Equal(new[] { 1, 2, 3, 4 }, new[] { Base.StatusConstants.NAME_GREEN, Base.StatusConstants.NAME_WHITE, Base.StatusConstants.NAME_RED, Base.StatusConstants.NAME_BLACK });
        Assert.Equal(7, Base.CutTrail(3, 7)); Assert.Equal(7, Base.CutTrail(7, 3));
        Assert.Equal(50, Base.AdjustDataEx(100, 30050)); Assert.Equal(3, Base.AdjustDataEx(100, -30003));
        Assert.Equal(2768, Base.AdjustDataEx(100, -32768, 999)); //<=-30000 shadows native AdjustFull branch.
        Assert.Equal(105, Base.AdjustDataEx(100, 5));
        Assert.Equal(5, Base.AdjustHitrate(10, 30050)); Assert.Equal(0, Base.AdjustHitrate(-20, 0));
        Assert.Equal(50, Base.AdjustAttack(100, 0, 50)); Assert.Equal(100, Base.AdjustAttack(100, 0, 0));
        Assert.Equal(50, Base.AdjustDefense(100, 30050)); Assert.Equal(2768, Base.AdjustDefense(100, -32768, 9));
        Assert.Equal(-2, Base.MulDiv(int.MaxValue, 2, 1)); Assert.Equal(-2L, Base.BigMulDiv(long.MaxValue, 2, 1));
        Assert.Equal(3L, Base.BigMulDiv(5, 1, 2)); Assert.Equal(-2L, Base.BigMulDiv(-5, 1, 2));
        Assert.Throws<DivideByZeroException>(() => Base.MulDiv(1, 2, 0)); Assert.Throws<DivideByZeroException>(() => Base.BigMulDiv(1, 2, 0));
    }

    [Fact]
    public Task BaseMinimumMonsterDamageReadsCurrentActualEquipmentRemaindersAndPlayerLevel() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Equipment!.LeftWeapon = 420009; s.Client.Equipment.RightWeapon = 410008;
        Assert.Equal(32, Base.AdjustMinDamageUser2Monster(1, s.Client)); Assert.Equal(100, Base.AdjustMinDamageUser2Monster(100, s.Client));
        s.Set("_level", (ushort)10); Assert.Equal(19, Base.AdjustMinDamageUser2Monster(1, s.Client));
        s.Client.Equipment = null; Assert.Throws<NullReferenceException>(() => Base.AdjustMinDamageUser2Monster(1, s.Client));
    });

    [Fact]
    public void BaseGeometryPreservesNarrowShortAndSignedIntSquareOverflowRatherThanWidening()
    {
        Assert.Equal((short)5, Base.GetDistance(1, 1, 4, 6)); Assert.Equal(5.0, Base.GetDDistance(0, 0, 3, 4));
        Assert.Equal((short)0, Base.GetDistance(0, 0, ushort.MaxValue, 0));
        Assert.Equal((short)-1, Base.GetDistance(0, 0, ushort.MaxValue, ushort.MaxValue));
        Assert.True(double.IsNaN(Base.GetDDistance(0, 0, ushort.MaxValue, 0)));
        Assert.Equal((short)0, Base.GetDistance(12, 34, 12, 34));
    }
}
