using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyQuestOwnerJournalTests
{
    [WindowsTradeJournalFailureFact]
    public void RealLateQuestReplacementFailureRestoresAllPreimagesAndAllowsFreshCommit()
    {
        var changes = Seed();
        FileStream? lease = null;
        var errors = new List<Exception>();
        try
        {
            // Full reads/preflight remain possible, but Windows denies ReplaceFile
            // on the actual Quest leaf after Character/Items were replaced.
            lease = new FileStream(Target(changes[2]), FileMode.Open, FileAccess.Read, FileShare.Read);
            using var interruption = LegacyJournalInterruption.Observe();
            Assert.False(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, interruption, out var reason));
            Assert.StartsWith("tradeJournalWriteFailed:", reason);
            Assert.Equal(new[] { 0, 1, 2 }, interruption.Visited);
            AssertImages(changes, after: false);
            Assert.False(LegacyTradeJournal.HasPending(Users));
        }
        catch (Exception error) { errors.Add(error); }
        try { lease?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);

        Assert.True(LegacyTradeJournal.TryCommitQuestOwner(Users, Items, Quests, changes, out var retryReason), retryReason);
        AssertImages(changes, after: true);
        Assert.False(LegacyTradeJournal.HasPending(Users));
    }
}
