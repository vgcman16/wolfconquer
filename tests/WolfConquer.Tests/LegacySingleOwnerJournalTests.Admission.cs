using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySingleOwnerJournalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItemsRootCannotBeThePendingJournalDirectoryOrItsDescendant(bool descendant)
    {
        var changes = SeedOwner(1_299_201);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, count => count == 0, out _));
        var evidence = File.ReadAllBytes(PendingPath);
        var journalRoot = Path.GetDirectoryName(PendingPath)!;
        var overlappingItemsRoot = descendant ? Path.Combine(journalRoot, "items") : journalRoot;

        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, overlappingItemsRoot, out var recovered, out var reason));

        Assert.Equal(0, recovered);
        Assert.StartsWith("tradeJournalRecoveryRequired:invalidOrUnreadable:ArgumentException", reason);
        AssertBefore(changes);
        Assert.Equal(evidence, File.ReadAllBytes(PendingPath));
        Assert.Empty(Directory.GetFiles(journalRoot, "*.bin", SearchOption.AllDirectories));
        if (descendant) Assert.False(Directory.Exists(overlappingItemsRoot));
        RecoverOnceAndAssertAfter(changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrNonmatchingConfiguredItemsRootCannotPartiallyReplayTheCharacter(bool existingForeignTarget)
    {
        var changes = SeedOwner(1_299_211);
        Assert.False(LegacyTradeJournal.TryCommitSingleOwner(UsersRoot, ItemsRoot, changes, count => count == 0, out _));
        var evidence = File.ReadAllBytes(PendingPath);
        var foreignItemsRoot = Path.Combine(root, "another-configured-items-root");
        var foreignItemPath = Path.Combine(foreignItemsRoot, Path.GetFileName(Target(changes[1])));
        var foreignBytes = new byte[] { 201, 202, 203, 204 };
        if (existingForeignTarget)
        {
            Directory.CreateDirectory(foreignItemsRoot);
            File.WriteAllBytes(foreignItemPath, foreignBytes);
        }

        // The record intentionally contains no original absolute-root identity.
        // This proves baseline rejection for missing/different bytes, not a claim
        // that an alternate root with identical images can be recognized as foreign.
        Assert.False(LegacyTradeJournal.TryRecover(UsersRoot, foreignItemsRoot, out var recovered, out var reason));

        Assert.Equal(0, recovered);
        Assert.StartsWith("tradeJournalRecoveryRequired:rollForwardFailed:", reason);
        AssertBefore(changes); // Character must not advance before all targets validate.
        Assert.Equal(evidence, File.ReadAllBytes(PendingPath));
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        if (existingForeignTarget) Assert.Equal(foreignBytes, File.ReadAllBytes(foreignItemPath));
        else Assert.False(Directory.Exists(foreignItemsRoot));

        RecoverOnceAndAssertAfter(changes);
        if (existingForeignTarget) Assert.Equal(foreignBytes, File.ReadAllBytes(foreignItemPath));
        else Assert.False(Directory.Exists(foreignItemsRoot));
    }
}
