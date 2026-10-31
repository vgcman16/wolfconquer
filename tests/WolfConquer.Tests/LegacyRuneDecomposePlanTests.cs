using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA ONLY: declared snapshots/rows test a pure plan, not a real UID issuer,
// admitted owner, producer, journal or client execution.
public sealed partial class LegacyRuneDecomposePlanTests
{
    private const uint TargetUid = 901, FragmentUid = 1001;

    [Theory]
    [InlineData(401U, false, 4_060_001U, 3_306_366U)]
    [InlineData(401U, true, 4_060_001U, 3_306_369U)]
    [InlineData(402U, false, 4_040_001U, 3_306_368U)]
    [InlineData(402U, true, 4_040_001U, 3_306_371U)]
    [InlineData(403U, false, 4_050_001U, 3_306_367U)]
    [InlineData(403U, true, 4_050_001U, 3_306_370U)]
    public void ActualMinimumRowsSelectTheExactFamilyAndBoundRewardSkus(uint family, bool bound, uint essence, uint fragment)
    {
        uint type = family switch { 401 => 4_010_101, 402 => 4_020_501, _ => 4_030_101 };
        var target = Target(type: type, bound: bound ? (byte)1 : (byte)0); var before = Items(target);
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(family, 1, 100), Caps());
        Assert.Same(target, plan.Target); Assert.Equal(80U, plan.RecycledProgress);
        Assert.Equal(essence, plan.EssenceItemId); Assert.Equal(new ushort[] { 8 }, plan.EssenceStacks);
        Assert.Equal(fragment, plan.FragmentItemId); Assert.Null(plan.FragmentStackUid);
        Assert.Equal((ushort)10, plan.FragmentStackSize); Assert.Equal(2, plan.NewUidCount);
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(24U, 99U, (ushort)9)] [InlineData(25U, 100U, (ushort)10)] [InlineData(150U, 200U, (ushort)20)]
    public void IntegerFloorPreservesFractionalBoundariesAndTheOldIntendedTwentyUnitReward(uint xp, uint recycled, ushort quantity)
    {
        var plan = Plan(Target(xp: xp)); Assert.Equal(recycled, plan.RecycledProgress);
        Assert.Equal(new[] { quantity }, plan.EssenceStacks);
    }

    [Theory]
    [InlineData(4_020_517U, (byte)17, 94000U, 75200U, new ushort[] { 7520 })]
    [InlineData(4_020_527U, (byte)27, 341500U, 273200U, new ushort[] { 10000, 10000, 7320 })]
    public void ActualHigherCurvesProduceCompleteCapBoundedEssenceRoots(uint type, byte level, uint progress, uint recycled, ushort[] stacks)
    {
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(Items(Target(type: type)), TargetUid, new(402, level, progress), Caps());
        Assert.Equal(recycled, plan.RecycledProgress); Assert.Equal(stacks, plan.EssenceStacks);
        Assert.All(plan.EssenceStacks, count => Assert.InRange(count, (ushort)1, (ushort)10000));
        Assert.Equal(stacks.Length + 1, plan.NewUidCount);
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)]
    public void OnePhysicalTargetRetainsItsRawZeroOrOneRepresentation(ushort stack)
    {
        ushort position = stack == 0 ? (ushort)102 : (ushort)122;
        var target = Target(stack: stack, position: position); var plan = Plan(target);
        Assert.Same(target, plan.Target); Assert.Equal(stack, plan.Target.StackSize);
        Assert.Equal(position, plan.Target.Position);
        Assert.Equal(new ushort[] { 8 }, plan.EssenceStacks);
    }

    [Fact]
    public void WholeFirstFitMergeSkipsNearFullRowsAndPreservesTheSelectedExistingRoot()
    {
        var skipped = Fragment(FragmentUid, 9995); var selected = Fragment(FragmentUid + 1, 9990, locked: 1);
        var later = Fragment(FragmentUid + 2, 1); var before = Items(Target(), [skipped, selected, later]);
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 1, 100), Caps());
        Assert.Equal(FragmentUid + 1, plan.FragmentStackUid); Assert.Equal((ushort)10000, plan.FragmentStackSize);
        Assert.Equal(1, plan.NewUidCount); Assert.Equal((ushort)9995, skipped.StackSize);
        Assert.Equal((ushort)9990, selected.StackSize); Assert.Equal((byte)1, selected.Locked);
        Assert.Equal((uint)6, selected.Color); Assert.Equal("retained", selected.Signature);
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Fact]
    public void RawZeroExistingFragmentCountsOneAndRequiresNoNewInventoryRoot()
    {
        var before = Items(Target(), [Fragment(FragmentUid, 0)]);
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 1, 100), Caps());
        Assert.Equal(FragmentUid, plan.FragmentStackUid); Assert.Equal((ushort)11, plan.FragmentStackSize);
        Assert.Equal(1, plan.NewUidCount); Assert.Equal((ushort)0, before.InventoryItems[0].StackSize);
    }

    [Fact]
    public void NearFullAndWrongBoundFragmentsAreNotPartiallyFilled()
    {
        var near = Fragment(FragmentUid, 9991); var wrongBound = Fragment(FragmentUid + 1, 1).WithBound(2);
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(Items(Target(), [near, wrongBound]), TargetUid, new(402, 1, 100), Caps());
        Assert.Null(plan.FragmentStackUid); Assert.Equal((ushort)10, plan.FragmentStackSize); Assert.Equal(2, plan.NewUidCount);
        Assert.Equal((ushort)9991, near.StackSize); Assert.Equal((ushort)1, wrongBound.StackSize);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualRequiredSlotsAllowAFullInventoryMergeOrOneFreeSlotForAFreshFragment(bool merge)
    {
        var inventory = FillInventory(39).ToList();
        if (merge) inventory.Add(Fragment(FragmentUid, 1));
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(Items(Target(), inventory), TargetUid, new(402, 1, 100), Caps());
        Assert.Equal(merge ? FragmentUid : (uint?)null, plan.FragmentStackUid);
        Assert.Equal(merge ? 1 : 2, plan.NewUidCount); Assert.Equal(merge ? 40 : 39, inventory.Count);
    }

    [Fact]
    public void RemovingASelectedBagRootFreesExactlyOneOfTheHundredEssenceSlots()
    {
        var before = Items(Target(position: 211), extraRunes: FillBag(99));
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(before, TargetUid, new(402, 1, 100), Caps());
        Assert.Single(plan.EssenceStacks); Assert.Equal(2, plan.NewUidCount);
        Assert.Equal(100, before.RuneItems.Count(row => row.Position == 211));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DataOnlyZeroProgressShapeCreatesNoPhantomEssenceOrClaimOfAnActualZeroSourceRow(bool merge)
    {
        // All37 actual401..403 rows have minimum100. This detached zero row is
        // only the explicit pure arithmetic shape, never a positive producer.
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(Items(Target(), merge ? new[] { Fragment(FragmentUid, 1) } : []), TargetUid, new(402, 1, 0), Caps());
        Assert.Equal(0U, plan.RecycledProgress); Assert.Empty(plan.EssenceStacks);
        Assert.Equal(merge ? (ushort)11 : (ushort)10, plan.FragmentStackSize); Assert.Equal(merge ? 0 : 1, plan.NewUidCount);
    }

    [Fact]
    public void RawZeroEssenceCapMeansOnePerRootRatherThanAnUnlimitedOrZeroSizedStack()
    {
        var caps = Caps(); caps[4_040_001] = new(4_040_001, 0);
        var plan = WorldPacketHandlers.PlanRecordedRuneDecompose(Items(Target()), TargetUid, new(402, 1, 100), caps);
        Assert.Equal(8, plan.EssenceStacks.Length); Assert.All(plan.EssenceStacks, count => Assert.Equal((ushort)1, count));
        Assert.Equal(9, plan.NewUidCount);
    }

    private static LegacyRuneDecomposePlan Plan(PlayerItemSnapshotItem target) =>
        WorldPacketHandlers.PlanRecordedRuneDecompose(Items(target), TargetUid, new(target.ItemId / 10000, checked((byte)(target.ItemId % 100)), 100), Caps());
    private static PlayerItemSnapshot Items(PlayerItemSnapshotItem target, IReadOnlyList<PlayerItemSnapshotItem>? inventory = null,
        IReadOnlyList<PlayerItemSnapshotItem>? extraRunes = null) => new(inventory ?? [], [], [], [], new[] { target }.Concat(extraRunes ?? []).ToArray());
    private static PlayerItemSnapshotItem Target(uint type = 4_020_501, uint xp = 0, ushort stack = 1, byte bound = 0,
        ushort position = 212, byte locked = 0, IReadOnlyList<PlayerItemSnapshotItem>? children = null) => new()
    { Uid = TargetUid, ItemId = type, RuneExperience = xp, StackSize = stack, Bound = bound, Position = position,
      Locked = locked, Durability = 10, MaximumDurability = 10, DepositedItems = children ?? [] };
    private static PlayerItemSnapshotItem Fragment(uint uid, ushort stack, byte locked = 0) => new()
    { Uid = uid, ItemId = 3_306_368, StackSize = stack, Position = 0, Bound = 1, Locked = locked,
      Durability = 1, MaximumDurability = 1, Color = 6, Signature = "retained" };
    private static PlayerItemSnapshotItem[] FillInventory(int count) => Enumerable.Range(0, count).Select(index => new PlayerItemSnapshotItem
    { Uid = 10000U + (uint)index, ItemId = 1_000_000, Position = 0, StackSize = 1 }).ToArray();
    private static PlayerItemSnapshotItem[] FillBag(int count) => Enumerable.Range(0, count).Select(index => new PlayerItemSnapshotItem
    { Uid = 2000U + (uint)index, ItemId = 4_040_001, Position = 211, StackSize = 1 }).ToArray();
    private static Dictionary<uint, LegacyRuneDecomposeRewardCap> Caps() => new uint[]
    { 4_040_001, 4_050_001, 4_060_001, 3_306_366, 3_306_367, 3_306_368, 3_306_369, 3_306_370, 3_306_371 }
        .ToDictionary(id => id, id => new LegacyRuneDecomposeRewardCap(id, 10000));
}
