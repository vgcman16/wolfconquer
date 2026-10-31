using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuneDecomposePlanTests
{
    [Theory]
    [InlineData("unknown-uid")] [InlineData("zero-uid")] [InlineData("duplicate-selected")]
    [InlineData("family")] [InlineData("level-zero")] [InlineData("red-too-high")]
    [InlineData("blue-too-high")] [InlineData("yellow-too-high")]
    [InlineData("stacked")] [InlineData("locked")] [InlineData("child")]
    [InlineData("misbound-family")] [InlineData("misbound-level")]
    [InlineData("wrong-slot")] [InlineData("source-total-overflow")]
    public void InvalidSelectedPhysicalStateAndCumulativeInputsCannotProduceARewardPlan(string fault)
    {
        var target = Target(); var progress = new LegacyRuneDecomposeProgressRow(402, 1, 100);
        uint selected = TargetUid;
        target = fault switch
        {
            "family" => Target(type: 4_040_001), "level-zero" => Target(type: 4_020_500),
            "red-too-high" => Target(type: 4_010_102), "blue-too-high" => Target(type: 4_020_528),
            "yellow-too-high" => Target(type: 4_030_110), "stacked" => Target(stack: 2),
            "locked" => Target(locked: 1), "child" => Target(children: [Fragment(FragmentUid, 1)]),
            "wrong-slot" => Target(position: 101), "source-total-overflow" => Target(xp: uint.MaxValue),
            _ => target,
        };
        if (fault == "unknown-uid") selected++;
        if (fault == "zero-uid") selected = 0;
        if (fault == "misbound-family") progress = new(403, 1, 100);
        if (fault == "misbound-level") progress = new(402, 2, 500);
        var before = Items(target, extraRunes: fault == "duplicate-selected" ? new[] { target } : []);
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneDecompose(before, selected, progress, Caps()));
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData("missing-essence")] [InlineData("missing-fragment")]
    [InlineData("misbound-essence")] [InlineData("misbound-fragment")]
    [InlineData("zero-fragment-cap")] [InlineData("small-fragment-cap")]
    public void ARewardNeedsItsExactCapAndAnEntireTenUnitFragmentStack(string fault)
    {
        var caps = Caps();
        if (fault == "missing-essence") caps.Remove(4_040_001);
        if (fault == "missing-fragment") caps.Remove(3_306_368);
        if (fault == "misbound-essence") caps[4_040_001] = new(4_050_001, 10000);
        if (fault == "misbound-fragment") caps[3_306_368] = new(3_306_371, 10000);
        if (fault == "zero-fragment-cap") caps[3_306_368] = new(3_306_368, 0);
        if (fault == "small-fragment-cap") caps[3_306_368] = new(3_306_368, 9);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneDecompose(Items(Target()), TargetUid, new(402, 1, 100), caps));
    }

    [Fact]
    public void AStoredTargetDoesNotFreeAnAlreadyFullEssenceBag()
    {
        var before = Items(Target(position: 212), extraRunes: FillBag(100));
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 1, 100), Caps()));
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(97, false)] [InlineData(98, true)]
    public void EverySplitEssenceRootCountsAgainstFinalBagCapacity(int count, bool refused)
    {
        var before = Items(Target(type: 4_020_527), extraRunes: FillBag(count));
        if (refused)
            Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 27, 341500), Caps()));
        else
        {
            var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 27, 341500), Caps());
            Assert.Equal(new ushort[] { 10000, 10000, 7320 }, plan.EssenceStacks); Assert.Equal(4, plan.NewUidCount);
        }
    }

    [Fact]
    public void FullInventoryWithoutAWholeFitFragmentRejectsBeforeAnySourceOrRewardMutation()
    {
        var inventory = FillInventory(39).Append(Fragment(FragmentUid, 9995)).ToArray();
        var before = Items(Target(), inventory); var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 1, 100), Caps()));
        Assert.Equal((ushort)9995, inventory[^1].StackSize); Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Fact]
    public void MaximumSourceAbiTotalAndEffectiveCapOneRefuseBeforeAnUnboundedSplitAllocation()
    {
        // Hostile DATA ONLY arithmetic, not a fabricated real catalog or owner.
        var caps = Caps(); caps[4_040_001] = new(4_040_001, 0);
        var before = Items(Target()); var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 1, uint.MaxValue), caps));
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }
}
