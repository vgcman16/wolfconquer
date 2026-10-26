using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [WindowsTradeJournalFailureFact]
    public Task RegisteredD13StackMergePreflightLeafFailureKeepsEveryQuantityAndUidStateThenSameOwnerRetries() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            fixture.HoldPackageJoinFile(fixture.ItemPath);
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            fixture.ReleaseFiles();
            AssertPackageJoinUnchanged(fixture, join, before);
            AssertNoPackageJoinIntents(join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 0);
        });

    [WindowsTradeJournalFailureFact]
    public Task RegisteredD13AllMergeJointReplaceFailureRetainsRealNoAllocationDebtThenFreshOwnerRetriesExactlyOnce() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            fixture.HoldStackReplaceTarget(fixture.CharacterPath);
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            fixture.ReleaseFiles();
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var after = ExpectedStackBusinessFiles(fixture, before, expected, [fixture.AtomicMailSeed], false, 1031);
            var pending = ExpectedStackPendingFiles(fixture, join, before, after);
            AssertPackageJoinFiles(fixture, pending);
            Assert.Same(before.Owner, join.Current);
            Assert.Equal(before.Json, System.Text.Json.JsonSerializer.Serialize(join.Current));
            Assert.Equal(before.FirstFrame, fixture.Sent.Count);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            AssertNoPackageJoinIntents(join);
            Assert.False(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out _));
            await join.RestartAsync();
            AssertPackageJoinFiles(fixture, pending);
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 0);
            await AssertStackReplayRetainsAsync(fixture, join, claimAll: false);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredD13StackMergePostcommitSendFailureRetainsExistingCreditsAndUniqueNewUids(bool mixedNew) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(mixedNew ? StackAllCompatible().Take(4) : StackAllCompatible()),
            async (fixture, join) =>
            {
                var before = CapturePackageJoin(fixture, join);
                PlayerItemSnapshot? expected = null;
                var attempts = 0;
                const string failure = "owned stack package postcommit send fault";
                join.SendObserver = _ =>
                {
                    var existing = StackCreditAll(before.Owner.PlayerItems.InventoryItems);
                    PlayerItemSnapshotItem[] fresh = mixedNew ?
                    [
                        ExpectedFreshStack(fixture, join, 2, 0, 3_316_793, 10),
                        ExpectedFreshStack(fixture, join, 2, 1, 3_330_770, 10),
                    ] : [];
                    expected = StackExpectedItems(before, [.. existing, .. fresh]);
                    AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, mixedNew ? 2 : 0);
                    attempts++;
                    throw new IOException(failure);
                };
                await join.FrameAsync(PackageJoinRequest(claimAll: false));
                join.SendObserver = null;
                Assert.Equal(1, attempts);
                Assert.Equal(before.FirstFrame + 1, fixture.Sent.Count);
                Assert.Contains(failure, fixture.Log.ToString());
                Assert.NotNull(expected);
                AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, mixedNew ? 2 : 0);
                await AssertStackReplayRetainsAsync(fixture, join, claimAll: false);
            });

    private sealed partial class Fixture
    {
        internal void HoldStackReplaceTarget(string path) =>
            heldFiles.Add(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }
}
