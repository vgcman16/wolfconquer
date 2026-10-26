using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public Task RegisteredD13AllMergePreparedOrPartialJointJournalReconcilesWithoutSecondCreditOrDummyUid(int replacements) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            var cut = replacements switch
            {
                0 => LegacyMailboxPackageInterruption.AfterPendingJournal,
                2 => LegacyMailboxPackageInterruption.AfterItems,
                4 => LegacyMailboxPackageInterruption.AfterCombatGear,
                _ => throw new ArgumentOutOfRangeException(nameof(replacements)),
            };
            Assert.False(prepared.Work.TryCommit(cut, out var proof, out var durable, out var reason));
            Assert.Null(proof);
            Assert.False(durable);
            Assert.Equal("mailboxClaimRecoveryRequired:packageJournal:tradeJournalRecoveryRequired:interrupted", reason);
            Assert.True(LegacyTradeJournal.HasPending(fixture.Users));
            Assert.Equal(before.Files[Path.GetRelativePath(fixture.Root, fixture.PackageUidState)], File.ReadAllBytes(fixture.PackageUidState));
            var interrupted = fixture.PackageFiles();
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            AssertPackageJoinFiles(fixture, interrupted);
            Assert.Equal(before.FirstFrame, fixture.Sent.Count);
            await join.RestartAsync();
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, newCount: 0);
            var firstReplay = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            Assert.Equal(firstReplay, fixture.Sent.Count);
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, newCount: 0);
        });
}
