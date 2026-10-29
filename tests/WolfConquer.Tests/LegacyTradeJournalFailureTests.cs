using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WindowsTradeJournalFailureFactAttribute : FactAttribute
{
    public WindowsTradeJournalFailureFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows file-sharing replacement/delete failures.";
    }
}

// Actual disposable-file sharing failures, not power-loss or multi-process proof.
public sealed class LegacyTradeJournalFailureTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-trade-journal-failure-" + Guid.NewGuid().ToString("N"));

    public LegacyTradeJournalFailureTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
    }

    [WindowsTradeJournalFailureFact]
    public void FailedSecondReplacementRestoresExactPreimagesAndAllowsFreshCommit()
    {
        var changes = SeedChanges();
        JournalInterruptionTestScope.Run(LegacyJournalInterruption.LeaseTargetAfter(1, 1), interruption =>
        {
            // The closed plan opens the actual declared target1 read handle
            // after replacement1, permitting reads but denying ReplaceFile.
            Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, interruption, out var reason));
            Assert.True(interruption.PendingObserved);
            Assert.Equal(changes[0].After, interruption.BoundaryImages[0]);

            Assert.StartsWith("tradeJournalWriteFailed:", reason);
            Assert.Equal(new[] { 0, 1 }, interruption.Visited);
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
            AssertAllBefore(changes);
        });

        Assert.True(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, out var retryReason), retryReason);
        AssertAfterAndRecoveryNoop(changes);
    }

    [WindowsTradeJournalFailureFact]
    public void UnreadableRollbackRetainsJournalAndRecoveryCanRetryAfterLockRelease()
    {
        var changes = SeedChanges();
        JournalInterruptionTestScope.Run(LegacyJournalInterruption.LeaseTargetAfter(1, 1, exclusive: true), interruption =>
        {
            Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, interruption, out var reason));
            Assert.Equal(changes[0].After, interruption.BoundaryImages[0]);

            Assert.StartsWith("tradeJournalRecoveryRequired:rollbackOrDeleteFailed:", reason);
            Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
            var journal = File.ReadAllBytes(PendingPath);
            Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out reason));
            Assert.Equal(0, recovered);
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
            Assert.Equal(journal, File.ReadAllBytes(PendingPath));
            Assert.Equal(changes[0].After, File.ReadAllBytes(Target(changes[0])));
        });

        AssertPrefix(changes, 1);
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var countRecovered, out var retryReason), retryReason);
        Assert.Equal(1, countRecovered);
        AssertAfterAndRecoveryNoop(changes);
    }

    [WindowsTradeJournalFailureFact]
    public void CommittedJournalDeleteFailureKeepsAfterImagesUntilRecoveryClearsRecord()
    {
        var changes = SeedChanges();
        JournalInterruptionTestScope.Run(LegacyJournalInterruption.LeasePendingAfter(4), interruption =>
        {
            Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, interruption, out var reason));
            for (var index = 0; index < changes.Length; index++) Assert.Equal(changes[index].After, interruption.BoundaryImages[index]);

            Assert.StartsWith("tradeJournalRecoveryRequired:committedRecordDeleteFailed:", reason);
            Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
            AssertAllAfter(changes); // A cleanup error must never reverse an already complete exchange.
            var journal = File.ReadAllBytes(PendingPath);
            Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out reason));
            Assert.Equal(0, recovered);
            Assert.StartsWith("tradeJournalRecoveryRequired:", reason);
            Assert.Equal(journal, File.ReadAllBytes(PendingPath));
            AssertAllAfter(changes);
        });

        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var countRecovered, out var retryReason), retryReason);
        Assert.Equal(1, countRecovered);
        AssertAfterAndRecoveryNoop(changes);
    }

    [WindowsTradeJournalFailureFact]
    public void PartiallyAppliedRecoveryRetainsRecordAndResumesWithoutDoubleCredit()
    {
        var changes = SeedChanges();
        Assert.False(LegacyTradeJournal.TryCommit(UsersRoot, ItemsRoot, changes, LegacyJournalInterruption.StopAfter(0), out _));
        var journal = File.ReadAllBytes(PendingPath);
        using (new FileStream(Target(changes[1]), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out var reason));
            Assert.Equal(0, recovered);
            Assert.StartsWith("tradeJournalRecoveryRequired:rollForwardFailed:", reason);
            Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
            Assert.Equal(journal, File.ReadAllBytes(PendingPath));
            AssertPrefix(changes, 1);
        }

        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var countRecovered, out var retryReason), retryReason);
        Assert.Equal(1, countRecovered);
        AssertAfterAndRecoveryNoop(changes);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string PendingPath => Path.Combine(UsersRoot, ".managed-trade-journal", "pending.json");

    private LegacyTradeJournalChange[] SeedChanges()
    {
        LegacyTradeJournalChange[] changes =
        [
            new(LegacyTradeJournalFileKind.Character, 1_296_001,
                Wallet(1_296_001, 1000, 100), Wallet(1_296_001, 1200, 125)),
            new(LegacyTradeJournalFileKind.Items, 1_296_001, [1, 2, 3], [4, 5, 6]),
            new(LegacyTradeJournalFileKind.Character, 1_296_002,
                Wallet(1_296_002, 2000, 200), Wallet(1_296_002, 1800, 175)),
            new(LegacyTradeJournalFileKind.Items, 1_296_002, [4, 5, 6], [1, 2, 3]),
        ];
        foreach (var change in changes) File.WriteAllBytes(Target(change), change.Before!);
        return changes;
    }

    private static byte[] Wallet(long uid, long money, uint cp) => Encoding.UTF8.GetBytes(
        FormattableString.Invariant($"[Character]\r\nUID={uid}\r\nMoney={money}\r\nConquerPoints={cp}\r\n"));

    private string Target(LegacyTradeJournalChange change) => Path.Combine(
        change.Kind == LegacyTradeJournalFileKind.Character ? UsersRoot : ItemsRoot,
        change.FileId.ToString(CultureInfo.InvariantCulture) +
        (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin"));

    private void AssertAllBefore(IEnumerable<LegacyTradeJournalChange> changes)
    {
        foreach (var change in changes) Assert.Equal(change.Before, File.ReadAllBytes(Target(change)));
    }

    private void AssertAllAfter(IEnumerable<LegacyTradeJournalChange> changes)
    {
        foreach (var change in changes) Assert.Equal(change.After, File.ReadAllBytes(Target(change)));
    }

    private void AssertPrefix(IReadOnlyList<LegacyTradeJournalChange> changes, int replacementCount)
    {
        for (var index = 0; index < changes.Count; index++)
            Assert.Equal(index < replacementCount ? changes[index].After : changes[index].Before,
                File.ReadAllBytes(Target(changes[index])));
    }

    private void AssertAfterAndRecoveryNoop(LegacyTradeJournalChange[] changes)
    {
        AssertAllAfter(changes);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var recovered, out var reason), reason);
        Assert.Equal(0, recovered);
        AssertAllAfter(changes); // Byte equality proves replay did not add the wallet credit again.
        Assert.Empty(Directory.GetFiles(root, "*.managed-trade-*.tmp", SearchOption.AllDirectories));
    }
}
