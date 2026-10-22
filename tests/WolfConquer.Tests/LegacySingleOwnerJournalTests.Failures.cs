using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySingleOwnerJournalTests
{
    [WindowsTradeJournalFailureFact]
    public void SecondReplacementSharingFailureRestoresBothOriginalFilesAndClearsJournal()
    {
        var changes = SeedOwner(1_299_101);
        FileStream? lease = null;
        try
        {
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, count =>
            {
                if (count != 1) return false;
                Assert.Equal(changes[0].After, Read(changes[0]));
                lease = new FileStream(Target(changes[1]), FileMode.Open, FileAccess.Read, FileShare.Read);
                return false;
            }, out var reason));
            Assert.StartsWith("tradeJournalWriteFailed:", reason);
            AssertBefore(changes);
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        }
        finally { lease?.Dispose(); }
        Assert.True(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, out var retryReason), retryReason);
        AssertAfter(changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InjectedIoOrSecurityFailureAfterBothReplacementsRollsBackBothExactPreimages(bool securityFailure)
    {
        var changes = SeedOwner(1_299_111);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, count =>
        {
            if (count == 2)
            {
                AssertAfter(changes);
                if (securityFailure) throw new System.Security.SecurityException("Injected completion failure.");
                throw new IOException("Injected completion failure.");
            }
            return false;
        }, out var reason));
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
        FileStream? lease = null;
        try
        {
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, count =>
            {
                if (count == 1) lease = new FileStream(Target(changes[1]), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }, out var reason));
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
            Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
            Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out reason));
            Assert.Equal(0, recovered);
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
        }
        finally { lease?.Dispose(); }
        RecoverOnceAndAssertAfter(changes);
    }

    [WindowsTradeJournalFailureFact]
    public void SingleOwnerCompletedRecordDeleteFailureRetainsAfterBytesWithoutDoubleCredit()
    {
        var changes = SeedOwner(1_299_131);
        FileStream? lease = null;
        try
        {
            Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, count =>
            {
                if (count == 2) lease = new FileStream(PendingPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return false;
            }, out var reason));
            Assert.StartsWith("tradeJournalRecoveryRequired:committedRecordDeleteFailed:", reason);
            AssertAfter(changes);
            Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        }
        finally { lease?.Dispose(); }
        RecoverOnceAndAssertAfter(changes);
    }
}
