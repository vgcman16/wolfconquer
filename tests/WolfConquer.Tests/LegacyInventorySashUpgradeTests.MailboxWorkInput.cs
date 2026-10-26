using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("null-row")]
    [InlineData("null-before")]
    [InlineData("null-after")]
    [InlineData("empty")]
    public Task AllocatingMailboxWorkRejectsIncompleteImagesWithoutLosingItsRealIntent(string fault) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var recipe = CreatePackageJoinRecipe(fixture, join, before);
            var producer = join.ReadDebtProducer();
            Assert.True(producer.TryReserveRecipe(recipe, out var receipt, out var admitted, out var reason), reason);
            Assert.True(admitted);
            Assert.NotNull(receipt);
            var intent = AssertPackageJoinAllocation(fixture, join);
            var after = ExpectedPackageJoinUnallocatedFiles(fixture, before, true, 10);
            after[Path.GetRelativePath(fixture.Root, fixture.ItemPath)] =
                PlayerItemSnapshotLoader.ToLegacyBytes(ExpectedPackageJoinItems(before, intent, true));
            var invalid = CorruptMailboxWorkImages(PackageJoinJournalChanges(fixture, before.Files, after), fault);
            var pending = CapturePackageJoin(fixture, join);

            Assert.False(producer.TryPrepareJointWork(recipe, receipt!, invalid, out var work, out reason));
            Assert.Null(work);
            Assert.NotEmpty(reason);
            AssertPackageJoinUnchanged(fixture, join, pending);
            Assert.Equal(intent.CopyPayload(), AssertPackageJoinAllocation(fixture, join).CopyPayload());

            await join.FrameAsync(PackageJoinRequest(true));
            AssertPackageJoinCommitted(fixture, join, before, true, 10);
        });

    [Theory]
    [InlineData("null-row")]
    [InlineData("null-before")]
    [InlineData("null-after")]
    [InlineData("empty")]
    public Task NoAllocationMailboxWorkRejectsIncompleteImagesWithoutChangingRetainedDebt(string fault) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            var invalid = CorruptMailboxWorkImages(PackageJoinJournalChanges(fixture, before.Files, prepared.After), fault);
            var pending = CapturePackageJoin(fixture, join);

            Assert.False(join.ReadDebtProducer().TryPrepareNoAllocationWork(prepared.Recipe, invalid,
                out var work, out _, out var reason));
            Assert.Null(work);
            Assert.NotEmpty(reason);
            AssertPackageJoinUnchanged(fixture, join, pending);
            AssertNoPackageJoinIntents(join);

            await join.RestartAsync();
            await join.FrameAsync(PackageJoinRequest(false));
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 0);
        });

    private static LegacyTradeJournalChange[] CorruptMailboxWorkImages(LegacyTradeJournalChange[] changes, string fault)
    {
        if (fault == "empty") return [];
        changes[0] = fault switch
        {
            "null-row" => null!,
            "null-before" => changes[0] with { Before = null },
            "null-after" => changes[0] with { After = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        return changes;
    }
}
