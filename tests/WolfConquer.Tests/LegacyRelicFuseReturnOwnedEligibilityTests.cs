using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Closed held-item DATA controls only. These snapshots confer no Runtime,
// saved-file, native, journal or session-store authority.
public sealed class LegacyRelicFuseReturnOwnedEligibilityTests
{
    private static PlayerItemSnapshotItem Row(int slot, uint itemId = 4_100_001,
        uint? uid = null, ushort stack = 1, byte bound = 0, byte locked = 0,
        byte suspicious = 0, IReadOnlyList<PlayerItemSnapshotItem>? children = null,
        bool nullChildren = false) => new()
    {
        Uid = uid ?? checked(10_000U + (uint)slot), ItemId = itemId,
        Position = checked((ushort)(213 + slot)), StackSize = stack, Bound = bound,
        Locked = locked, Suspicious = suspicious, Durability = 6_000, MaximumDurability = 7_000,
        Attribute1 = 100_003, Attribute2 = 110_004, Attribute3 = 120_005,
        Attribute4 = 130_009, Attribute5 = 100_114,
        DepositedItems = nullChildren ? null! : children ?? [],
    };

    private static PlayerItemSnapshotItem[] Triple() => [Row(0), Row(1), Row(2)];
    private static PlayerItemSnapshot Snapshot(params PlayerItemSnapshotItem[] rows) =>
        PlayerItemSnapshot.Empty with { RelicFuseItems = rows };

    [Theory]
    [InlineData(4_100_001U)] [InlineData(4_100_002U)] [InlineData(4_100_003U)]
    [InlineData(4_100_004U)] [InlineData(4_100_005U)]
    public void EveryKnownFirstAndSecondItemPairRetainsTheFirstItemTypeInTheResult(uint itemId)
    {
        foreach (var secondId in new uint[] { 4_100_001, 4_100_002, 4_100_003, 4_100_004, 4_100_005 })
        {
            var rows = new[] { Row(0, itemId), Row(1, secondId), Row(2, itemId) };
            Assert.True(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
        }
    }

    [Theory]
    [InlineData(0, 0, 0, 0)] [InlineData(0, 0, 0, 1)] [InlineData(0, 0, 1, 1)]
    [InlineData(0, 1, 0, 1)] [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 0, 0)] [InlineData(1, 0, 0, 1)] [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)] [InlineData(1, 1, 1, 1)]
    public void LegacyUnitQuantitiesAndEverySafeBindingCombinationRemainEligible(
        int stack, int firstBound, int secondBound, int resultBound)
    {
        var rows = new[] { Row(0, stack: (ushort)stack, bound: (byte)firstBound),
            Row(1, stack: (ushort)stack, bound: (byte)secondBound), Row(2, stack: (ushort)stack, bound: (byte)resultBound) };
        var snapshot = Snapshot(rows);
        var before = PlayerItemSnapshotLoader.ToLegacyBytes(snapshot);
        Assert.True(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(snapshot));
        Assert.Equal(before, PlayerItemSnapshotLoader.ToLegacyBytes(snapshot));
    }

    [Fact]
    public void AResultOfAnotherKnownItemTypeCannotRepresentTheActualComposeOutput()
    {
        var rows = Triple(); rows[2] = Row(2, 4_100_002);
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(0, 1)] [InlineData(1, 1)]
    public void ATradableResultCannotHideBindingOnEitherSource(int firstBound, int secondBound)
    {
        var rows = new[] { Row(0, bound: (byte)firstBound), Row(1, bound: (byte)secondBound), Row(2, bound: 0) };
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
    }

    [Fact]
    public void CompleteUniquePositionsDoNotRequireAnIncidentalListOrder()
    {
        var rows = Triple(); Array.Reverse(rows);
        Assert.True(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
    }

    [Theory]
    [InlineData("zero-uid")] [InlineData("locked")] [InlineData("suspicious")]
    [InlineData("bound-width")] [InlineData("stacked")] [InlineData("children")]
    [InlineData("null-children")] [InlineData("null-row")]
    public void EveryHeldRowIncludingTheFinalTailMustRetainTheOwnedComposeShape(string defect)
    {
        for (var slot = 0; slot < 3; slot++)
        {
            var rows = Triple();
            rows[slot] = defect switch
            {
                "zero-uid" => Row(slot, uid: 0),
                "locked" => Row(slot, locked: 1),
                "suspicious" => Row(slot, suspicious: 1),
                "bound-width" => Row(slot, bound: 2),
                "stacked" => Row(slot, stack: 2),
                "children" => Row(slot, children: [new() { Uid = 20_000, ItemId = 1_000_000 }]),
                "null-children" => Row(slot, nullChildren: true),
                _ => null!,
            };
            Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
        }
    }

    [Theory]
    [InlineData(0U)] [InlineData(4_100_000U)] [InlineData(4_100_006U)] [InlineData(uint.MaxValue)]
    public void InvalidOrForeignItemTypesCannotHideAtAnyHeldPosition(uint itemId)
    {
        for (var slot = 0; slot < 3; slot++)
        {
            var rows = Triple(); rows[slot] = Row(slot, itemId);
            Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(212)] [InlineData(216)] [InlineData(ushort.MaxValue)]
    public void AForeignPositionCannotReplaceAnyMemberOfTheHeldTriple(int position)
    {
        for (var slot = 0; slot < 3; slot++)
        {
            var rows = Triple(); rows[slot] = rows[slot].WithPosition((ushort)position);
            Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
        }
    }

    [Theory]
    [InlineData(44U)] [InlineData(100U)]
    public void AStoredItemCannotBeUsedAsAnyHeldRoot(uint warehouse)
    {
        for (var slot = 0; slot < 3; slot++)
        {
            var rows = Triple(); rows[slot] = rows[slot].WithWarehouseId(warehouse);
            Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
        }
    }

    [Fact]
    public void DuplicateIdentityPositionOrActualRowAliasRefusesTheCompleteTriple()
    {
        var rows = Triple(); rows[2] = Row(2, uid: rows[0].Uid);
        Assert.NotSame(rows[0], rows[2]);
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
        rows = Triple(); rows[2] = rows[2].WithPosition(rows[0].Position);
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
        rows = Triple(); rows[2] = rows[0];
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public void MissingOrExtraHeldRootsCannotFormAThreeChoiceReturn(int count)
    {
        var rows = Enumerable.Range(0, count).Select(slot => Row(slot)).ToArray();
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(Snapshot(rows)));
    }

    [Fact]
    public void MissingRequiredSnapshotOrHeldCollectionRefuses()
    {
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(null!));
        Assert.False(LegacyRelicFuseReturnOwnedEvidence.HeldItemsAreEligible(
            PlayerItemSnapshot.Empty with { RelicFuseItems = null! }));
    }
}
