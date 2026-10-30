using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuneChancePlanTests
{
    [Theory]
    [InlineData(0, true)] [InlineData(9, true)]
    [InlineData(10, false)] [InlineData(99, false)]
    public void OneCapturedRollUsesTheIntegerThresholdAndConsumesOnlyProgress(byte roll, bool passed)
    {
        var before = Items(1500);
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var plan = WorldPacketHandlers.PlanRecordedRuneChance(before, 901, Curves(), roll);
        Assert.True(plan.Gambled);
        Assert.Equal(10, plan.RatePercent);
        Assert.Equal(passed, plan.Passed);
        Assert.Equal(passed ? 4_020_518U : 4_020_517U, plan.Target.ItemId);
        Assert.Equal(0U, plan.Target.RuneExperience);
        Assert.Equal(901U, plan.Target.Uid);
        Assert.Equal((ushort)212, plan.Target.Position);
        Assert.Equal((ushort)1, plan.Target.StackSize);
        Assert.Equal((ushort)31, plan.Target.Durability);
        Assert.True(plan.UsedProgress.SetEquals(new[] { (402U, (byte)17), (402U, (byte)18) }));
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(149U, 0, false)] [InlineData(15000U, 100, true)]
    public void DeterministicRateBoundariesNeedNoDraw(uint experience, int rate, bool passed)
    {
        var plan = WorldPacketHandlers.PlanRecordedRuneChance(Items(experience), 901, Curves(), null);
        Assert.True(plan.Gambled); Assert.Equal(rate, plan.RatePercent); Assert.Equal(passed, plan.Passed);
        Assert.Equal(0U, plan.Target.RuneExperience);
        Assert.Equal(passed ? 4_020_518U : 4_020_517U, plan.Target.ItemId);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ZeroProgressOrMaximumLevelNeedsNoCurveOrDrawAndPreservesTheExactItem(bool maximum)
    {
        var before = Items(maximum ? 7U : 0U, maximum ? 4_020_527U : 4_020_517U);
        var item = Assert.Single(before.RuneItems);
        var plan = WorldPacketHandlers.PlanRecordedRuneChance(before, 901, new Dictionary<(uint, byte), LegacyRuneChanceProgressRow>(), null);
        Assert.Same(item, plan.Target); Assert.False(plan.Gambled); Assert.False(plan.Passed);
        Assert.Empty(plan.UsedProgress);
    }

    [Theory]
    [InlineData(15001U)] [InlineData(uint.MaxValue)]
    public void OverfilledProgressCannotWrapItsRateOrAwardAnUpgrade(uint experience)
    {
        var before = Items(experience); var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneChance(before, 901, Curves(), null));
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(null)] [InlineData((byte)100)] [InlineData(byte.MaxValue)]
    public void AProbabilisticAttemptRequiresOneBoundedCapturedRoll(byte? roll)
    {
        var before = Items(1500);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneChance(before, 901, Curves(), roll));
        Assert.Equal(1500U, Assert.Single(before.RuneItems).RuneExperience);
    }

    [Theory]
    [InlineData("missing")] [InlineData("equal")] [InlineData("decreasing")] [InlineData("wrong-identity")]
    public void MissingOrInvalidCurvesRefuseBeforeProgressChanges(string fault)
    {
        var curves = Curves();
        if (fault == "missing") curves.Remove((402, 18));
        else curves[(402, 18)] = fault switch
        {
            "equal" => new(402, 18, 94000),
            "decreasing" => new(402, 18, 93999),
            _ => new(403, 18, 109000),
        };
        var before = Items(1500); var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneChance(before, 901, curves, 0));
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    private static PlayerItemSnapshot Items(uint experience, uint itemId = 4_020_517) => new([], [], [], [],
        [new PlayerItemSnapshotItem { Uid = 901, ItemId = itemId, Position = 212, StackSize = 1,
            Durability = 31, MaximumDurability = 50, RuneExperience = experience }]);

    private static Dictionary<(uint Family, byte Level), LegacyRuneChanceProgressRow> Curves() => new()
    { [(402, 17)] = new(402, 17, 94000), [(402, 18)] = new(402, 18, 109000) };
}
