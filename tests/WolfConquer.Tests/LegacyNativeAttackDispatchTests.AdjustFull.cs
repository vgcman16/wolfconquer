using Base = WolfConquer.Core.World.LegacyNativeAttackBase;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Theory]
    [InlineData(100, 0, 0)]
    [InlineData(100, 999, 999)]
    [InlineData(int.MinValue, 42, 42)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
    [InlineData(100, int.MinValue, int.MinValue)]
    public void AdjustFullReturnsTheSuppliedMaximumInsteadOfAnAbsoluteSetAmount(int data, int maximum, int expected)
    {
        Assert.Equal(expected, Base.AdjustDataEx(data, Base.StatusConstants.AdjustFull, maximum));
    }

    [Theory]
    [InlineData(-32767, 2767)]
    [InlineData(-30001, 1)]
    [InlineData(-30000, 0)]
    [InlineData(-29999, -29899)]
    [InlineData(29999, 30099)]
    [InlineData(30000, 0)]
    [InlineData(30050, 50)]
    [InlineData(30100, 100)]
    public void AdjustFullCorrectionDoesNotNormalizeNeighboringSetPercentOrAddBranches(int adjust, int expected)
    {
        Assert.Equal(expected, Base.AdjustDataEx(100, adjust, 999));
    }

    [Fact]
    public void FullSentinelPropagatesThroughTheActualAdjustmentWrappers()
    {
        Assert.Equal(0, Base.AdjustDataEx(100, Base.StatusConstants.AdjustFull));
        Assert.Equal(0, Base.AdjustHitrate(100, Base.StatusConstants.AdjustFull));
        Assert.Equal(9, Base.AdjustDefense(100, Base.StatusConstants.AdjustFull, 9));
        Assert.Equal(0, Base.AdjustDefense(100, Base.StatusConstants.AdjustFull, -1));
        Assert.Equal(0, Base.AdjustAttack(100, Base.StatusConstants.AdjustFull, 50));
        Assert.Equal(0, Base.AdjustAttack(100, Base.StatusConstants.AdjustFull, 0));
    }
}
