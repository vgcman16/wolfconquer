using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(1000, 900, 1)]
    [InlineData(150, 100, 2)]
    public Task RegisteredD13PackageDoesNotPartiallyTopOffAnExistingStackWhenWholeGrantDoesNotFit(
        int limit, int current, int newCount) => RunStackPackageAsync(fixture =>
        {
            var rows = StackAllCompatible();
            rows[1] = rows[1].WithStackSize(checked((ushort)current));
            fixture.SetStackInventory(rows);
        }, async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            var fresh = Enumerable.Range(0, newCount).Select(index => ExpectedFreshStack(fixture, join, newCount, index,
                4_050_001, checked((ushort)(newCount == 1 ? 200 : index == 0 ? 150 : 50)))).ToArray();
            var existing = before.Owner.PlayerItems.InventoryItems.Select(row => row.ItemId == 4_050_001 ? row :
                row.WithStackSize(checked((ushort)(row.StackSize + RankOnePackageRows.Single(grant => grant.Id == row.ItemId).Quantity)))).ToArray();
            var expected = StackExpectedItems(before, [.. existing, .. fresh]);
            var outputs = new List<StackItemOutput>();
            foreach (var grant in RankOnePackageRows)
            {
                var phase = (byte)(grant.Id is 3_316_793 or 3_330_770 ? 2 : 0);
                if (grant.Id == 4_050_001) outputs.AddRange(fresh.Select(row => new StackItemOutput(phase, row, LegacyGameItemPacket.AddItemMode)));
                else outputs.Add(new(phase, Assert.Single(existing, row => row.ItemId == grant.Id), LegacyGameItemPacket.UpdateItemMode));
            }
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, newCount);
            AssertStackFrames(fixture, before, [new(fixture.AtomicMailSeed, outputs.ToArray(), 1031)],
                false, started, DateTime.Now, before.FirstFrame);
            await AssertStackReplayRetainsAsync(fixture, join, claimAll: false);
        }, StackPackageCatalog(4_050_001, checked((ushort)limit)));

    [Fact]
    public Task RegisteredD13PackageAllocatesOnlyTwoMissingTailStacksAndRetainsTheirRealColorsOnReplay() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible().Take(4)), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            var first = StackCreditAll(before.Owner.PlayerItems.InventoryItems);
            var tail = new[]
            {
                ExpectedFreshStack(fixture, join, 2, 0, 3_316_793, 10),
                ExpectedFreshStack(fixture, join, 2, 1, 3_330_770, 10),
            };
            var expected = StackExpectedItems(before, [.. first, .. tail]);
            var outputs = StackOutputs(first).Concat(tail.Select(row =>
                new StackItemOutput(2, row, LegacyGameItemPacket.AddItemMode))).ToArray();
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 2);
            AssertStackFrames(fixture, before, [new(fixture.AtomicMailSeed, outputs, 1031)], false, started, DateTime.Now, before.FirstFrame);
            await AssertStackReplayRetainsAsync(fixture, join, claimAll: false);
        });

    [Theory]
    [InlineData("plus", false)]
    [InlineData("locked", false)]
    [InlineData("plus", true)]
    [InlineData("locked", true)]
    public Task RegisteredD13PackageNeverOverwritesEnhancedOrLockedExistingStackToMakeItFit(string fault, bool full) =>
        RunStackPackageAsync(fixture =>
        {
            var rows = StackAllCompatible();
            rows[1] = fault == "plus" ? rows[1].WithMaximumDurabilityAndPlus(321, 1) : rows[1].WithLocked(1);
            fixture.SetStackInventory(rows, fillTo: full ? 40 : 0);
        }, async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            if (full)
            {
                AssertPackageJoinUnchanged(fixture, join, before, extraFrames: 1);
                Assert.Equal(LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes(), fixture.Sent[^1]);
                AssertNoPackageJoinIntents(join);
                return;
            }
            var fresh = ExpectedFreshStack(fixture, join, 1, 0, 4_050_001, 200);
            var existing = before.Owner.PlayerItems.InventoryItems.Select(row => row.ItemId == 4_050_001 ? row :
                row.WithStackSize(checked((ushort)(row.StackSize + RankOnePackageRows.Single(grant => grant.Id == row.ItemId).Quantity)))).ToArray();
            var expected = StackExpectedItems(before, [.. existing, fresh]);
            var outputs = RankOnePackageRows.Select((grant, index) => new StackItemOutput((byte)(index < 4 ? 0 : 2),
                grant.Id == 4_050_001 ? fresh : Assert.Single(existing, row => row.ItemId == grant.Id),
                grant.Id == 4_050_001 ? LegacyGameItemPacket.AddItemMode : LegacyGameItemPacket.UpdateItemMode)).ToArray();
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 1);
            AssertStackFrames(fixture, before, [new(fixture.AtomicMailSeed, outputs, 1031)], false, started, DateTime.Now, before.FirstFrame);
        });
}
