using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA ONLY: these snapshots/caps exercise pure selection/projection. They do
// not establish an admitted owner, catalog adoption, UID issuer or real WAL.
public sealed partial class LegacyRuneBagPlanTests
{
    private const uint TargetUid = 901, FragmentType = 3_306_366;

    [Theory]
    [InlineData(4_020_517U, (ushort)0)]
    [InlineData(FragmentType, (ushort)10000)]
    [InlineData(1_000_000U, (ushort)0)]
    public void IngressAcceptsAnyValidOwnedRootTypeWithoutInventingARuneAllowlist(uint type, ushort cap)
    {
        var target = Row(TargetUid, type, 0); var before = Items(inventory: [target]);
        var original = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var plan = WorldPacketHandlers.PlanRecordedRuneBag(before, TargetUid, true, new(type, cap));
        Assert.Same(target, plan.Target); Assert.True(plan.IntoBag);
        Assert.Null(plan.MergeUid); Assert.Null(plan.MergedStackSize); Assert.Equal(0, plan.NewUidCount);
        Assert.Equal(original, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)]
    public void AStoredNonstackRootIsOnePhysicalUnitWhileItsRawRepresentationIsRetained(ushort stack)
    {
        var target = Row(TargetUid, 4_020_517, stack: stack);
        var plan = Plan(Items(runes: [target]), target, 0);
        Assert.Same(target, plan.Target); Assert.Equal(stack, plan.Target.StackSize);
        Assert.Null(plan.MergeUid); Assert.Equal(1, plan.NewUidCount);
    }

    [Theory]
    [InlineData(4_020_517U, (ushort)0, (ushort)2)]
    [InlineData(FragmentType, (ushort)10, (ushort)11)]
    [InlineData(350001U, (ushort)10000, (ushort)2)]
    public void SelectedOvercapRowsRefuseIncludingTheSourceAccessoryCapOneOverride(uint type, ushort cap, ushort stack)
    {
        var target = Row(TargetUid, type, stack: stack); var before = Items(runes: [target]);
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => Plan(before, target, cap));
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Fact]
    public void WholeCompatibleMergeSelectsTheLowestPhysicalUidRatherThanArrayOrder()
    {
        var target = Row(TargetUid, stack: 2);
        var first = Row(90, position: 0, stack: 3);
        var lower = Row(10, position: 0, stack: 8);
        var middle = Row(50, position: 0, stack: 4);
        var before = Items([first, middle, lower], [target]);
        var original = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var plan = Plan(before, target, 10);
        Assert.Equal(10U, plan.MergeUid); Assert.Equal((ushort)10, plan.MergedStackSize);
        Assert.Equal(0, plan.NewUidCount); Assert.Equal((byte)1, lower.Locked);
        Assert.Equal(original, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData("locked")] [InlineData("expiry")] [InlineData("experience")] [InlineData("perfection")]
    public void IncompatiblePayloadTakesTheLosslessRekeyBranchInsteadOfClearingOrDiscardingIt(string field)
    {
        var target = Row(TargetUid, stack: 2); var receiver = Row(50, position: 0, stack: 3);
        receiver = field switch
        {
            "locked" => receiver.WithLocked(0),
            "expiry" => receiver.WithActivation(1, 638_000_000_000_000_000),
            "experience" => receiver.WithRuneExperience(1234),
            _ => receiver,
        };
        if (field == "perfection") receiver.PerfectionProgress = 999;
        var before = Items([receiver], [target]); var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var plan = Plan(before, target, 10);
        Assert.Null(plan.MergeUid); Assert.Null(plan.MergedStackSize); Assert.Equal(1, plan.NewUidCount);
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void EitherRootsNonemptyChildClosurePreventsAMergeAndKeepsItsPhysicalUids(bool selectedHasChildren)
    {
        var target = Row(TargetUid, stack: 2); var receiver = Row(50, position: 0, stack: 3);
        var child = Row(77, 1_000_000, 0);
        if (selectedHasChildren) target = target.WithDepositedItems([child]);
        else receiver = receiver.WithDepositedItems([child]);
        var before = Items([receiver], [target]); var original = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var plan = Plan(before, target, 10);
        Assert.Null(plan.MergeUid); Assert.Equal(1, plan.NewUidCount);
        Assert.Equal(original, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Fact]
    public void FullInventoryCanReceiveAWholeCompatibleStackWithoutANewSlotOrUid()
    {
        var target = Row(TargetUid, stack: 3);
        var inventory = FillInventory().ToArray(); inventory[19] = Row(50, position: 0, stack: 4);
        var plan = Plan(Items(inventory, [target]), target, 10);
        Assert.Equal(50U, plan.MergeUid); Assert.Equal((ushort)7, plan.MergedStackSize); Assert.Equal(0, plan.NewUidCount);
    }

    [Fact]
    public void FullInventoryCannotDeleteAChildBearingRootOrUseAnUnjoinedReturnedWarehouse()
    {
        var child = Row(77, 1_000_000, 0);
        var target = Row(TargetUid, stack: 3).WithDepositedItems([child]);
        var inventory = FillInventory().ToArray(); inventory[19] = Row(50, position: 0, stack: 4);
        var before = Items(inventory, [target]); var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        Assert.Throws<InvalidOperationException>(() => Plan(before, target, 10));
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(99, true)] [InlineData(100, false)]
    public void BagCapacityCountsOnlyPhysicalPosition211Roots(int count, bool accepted)
    {
        var target = Row(TargetUid, 1_000_000, 0);
        var runes = Enumerable.Range(0, count).Select(index => Row((uint)(2000 + index), 1_000_000)).ToList();
        runes.Add(Row(3000, 4_020_517, 212));
        var before = Items([target], runes.ToArray()); var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        if (accepted) Assert.Equal(0, WorldPacketHandlers.PlanRecordedRuneBag(before, TargetUid, true, new(target.ItemId, 0)).NewUidCount);
        else Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneBag(before, TargetUid, true, new(target.ItemId, 0)));
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DuplicatePhysicalIdentityAnywhereInTheBeforeGraphCannotBecomeSelectionAuthority(int location)
    {
        var target = Row(TargetUid);
        var before = location switch
        {
            0 => Items([Row(TargetUid, position: 0)], [target]),
            1 => Items([Row(77, position: 0)], [target.WithDepositedItems([Row(77, 1_000_000, 0)])]),
            _ => Items(runes: [target]) with { WarehouseItems = [Row(TargetUid, position: 0).WithWarehouseId(1000)] },
        };
        Assert.Throws<InvalidOperationException>(() => Plan(before, target, 10));
    }

    private static LegacyRuneBagPlan Plan(PlayerItemSnapshot before, PlayerItemSnapshotItem target, ushort cap) =>
        WorldPacketHandlers.PlanRecordedRuneBag(before, target.Uid, false, new(target.ItemId, cap));

    private static PlayerItemSnapshot Items(PlayerItemSnapshotItem[]? inventory = null, PlayerItemSnapshotItem[]? runes = null) =>
        new(inventory ?? [], [], [], [], runes ?? []);

    private static IEnumerable<PlayerItemSnapshotItem> FillInventory() =>
        Enumerable.Range(0, 40).Select(index => Row((uint)(100 + index), 1_000_000, 0));

    private static PlayerItemSnapshotItem Row(uint uid, uint type = FragmentType, ushort position = 211,
        ushort stack = 1, byte locked = 1) => new()
    {
        Uid = uid, ItemId = type, Position = position, StackSize = stack,
        Durability = 100, MaximumDurability = 100, Bound = 1, Locked = locked,
        Color = 6, OwnerName = "keeper", Signature = "retain",
    };
}
