using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySingleOwnerJournalTests
{
    [WindowsTradeJournalFailureFact]
    public void SecondReplacementSharingFailureRestoresBothOriginalFilesAndClearsJournal()
    {
        var changes = SeedOwner(1_299_101);
        JournalInterruptionTestScope.Run(LegacyJournalInterruption.LeaseTargetAfter(1, 1), interruption =>
        {
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, interruption, out var reason));
            Assert.Equal(changes[0].After, interruption.BoundaryImages[0]);
            Assert.StartsWith("tradeJournalWriteFailed:", reason);
            AssertBefore(changes);
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        });
        Assert.True(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, out var retryReason), retryReason);
        AssertAfter(changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InjectedIoOrSecurityFailureAfterBothReplacementsRollsBackBothExactPreimages(bool securityFailure)
    {
        var changes = SeedOwner(1_299_111);
        using var interruption = LegacyJournalInterruption.ThrowAfter(2,
            securityFailure ? LegacyJournalFault.ThrowSecurity : LegacyJournalFault.ThrowIo);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, interruption, out var reason));
        for (var index = 0; index < changes.Length; index++)
        {
            Assert.Equal(changes[index].After, interruption.BoundaryImages[index]);
        }
        Assert.StartsWith("tradeJournalWriteFailed:", reason);
        AssertBefore(changes);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out reason), reason);
        Assert.Equal(0, recovered);
        AssertBefore(changes);
    }

    [WindowsTradeJournalFailureFact]
    public void UncertainSingleOwnerRollbackRetainsRecordUntilTheFileCanBeRecovered()
    {
        var changes = SeedOwner(1_299_121);
        JournalInterruptionTestScope.Run(LegacyJournalInterruption.LeaseTargetAfter(1, 1, exclusive: true), interruption =>
        {
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, interruption, out var reason));
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
            Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
            Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out reason));
            Assert.Equal(0, recovered);
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        });
        RecoverOnceAndAssertAfter(changes);
    }

    [WindowsTradeJournalFailureFact]
    public void SingleOwnerCompletedRecordDeleteFailureRetainsAfterBytesWithoutDoubleCredit()
    {
        var changes = SeedOwner(1_299_131);
        JournalInterruptionTestScope.Run(LegacyJournalInterruption.LeasePendingAfter(2), interruption =>
        {
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, interruption, out var reason));
            for (var index = 0; index < changes.Length; index++) Assert.Equal(changes[index].After, interruption.BoundaryImages[index]);
            Assert.StartsWith("tradeJournalRecoveryRequired:committedRecordDeleteFailed:", reason);
            AssertAfter(changes);
            Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        });
        RecoverOnceAndAssertAfter(changes);
    }
}
