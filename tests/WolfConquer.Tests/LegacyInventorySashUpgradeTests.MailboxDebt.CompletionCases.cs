using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task ActualJointCommitBeforeCompletionMarkReconcilesFourExactPostimagesAndAllowsNormalFreshAdmissionWithoutRepayment() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            var recipe = CreatePackageJoinRecipe(fixture, join, initial);
            var producer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            var after = DebtAfterFiles(fixture, initial, intent);
            var changes = PackageJoinJournalChanges(fixture, initial.Files, after);
            Assert.True(producer.TryPrepareJointWork(recipe, intent.Allocation!, changes, out var work, out var reason), reason);
            AssertDebtWork(fixture, join, initial, intent, after, completed: false);

            Assert.False(work!.TryCommit(LegacyMailboxPackageInterruption.AfterJointCommit, out var proof, out var durable, out reason));
            Assert.Null(proof);
            Assert.True(durable);
            Assert.Equal("mailboxClaimRecoveryRequired:packageCompletionInterrupted", reason);
            foreach (var path in new[] { fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath })
                Assert.Equal(after[Path.GetRelativePath(fixture.Root, path)], File.ReadAllBytes(path));
            AssertDebtWork(fixture, join, initial, intent, after, completed: false);
            Assert.Equal(initial.FirstFrame, fixture.Sent.Count);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
            var workBefore = File.ReadAllBytes(DebtWorkPath(fixture, join, intent.Request.OperationId));
            var uidBefore = File.ReadAllBytes(fixture.PackageUidState);
            await join.RestartAsync();

            AssertDebtPackageCommitted(fixture, join, initial, intent);
            Assert.Equal(uidBefore, File.ReadAllBytes(fixture.PackageUidState));
            Assert.NotEqual(workBefore, File.ReadAllBytes(DebtWorkPath(fixture, join, intent.Request.OperationId)));
            var freshProducer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            Assert.True(freshProducer.TryRequireAccountWritable(fixture.Account, out reason), reason);
            var committed = fixture.PackageFiles();
            var replay = fixture.Sent.Count;
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(claimAll: true), direct: true);
            AssertPackageJoinFiles(fixture, committed);
            AssertPackageJoinReplay(fixture, claimAll: true, replay);
            AssertDebtPackageCommitted(fixture, join, initial, intent);
            await AssertDebtNormalMoneyAllowedAsync(fixture, join);
        });
}
