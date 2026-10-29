using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitMailboxOwner(string usersRoot, string itemsRoot, string mailboxRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, out string reason) =>
        TryCommitMailboxOwner(usersRoot, itemsRoot, mailboxRoot, changes, null, out reason);

    // v4 uses the same pending slot/replacement/rollback/recovery engine.
    // Genuine failure-only prefixes: 0 published, 1 Character, 2 Items, 3 Mailbox.
    internal static bool TryCommitMailboxOwner(string usersRoot, string itemsRoot, string mailboxRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyJournalInterruption? interrupt, out string reason)
    {
        reason = string.Empty;
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            var roots = GetMailboxRoots(usersRoot, itemsRoot, mailboxRoot);
            var record = CreateRecord(changes, 4) with
            { MailboxDomain = new(roots.Users, roots.Items, roots.Mailbox!) };
            var targets = GetTargets(roots, record);
            var failure = string.Empty;
            var result = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(),
                () => CommitLocked(roots, record, targets, interrupt, out failure));
            reason = failure;
            return result;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "tradeJournalInputInvalid:" + error.GetType().Name; return false; }
    }

    private static void ValidateMailboxChanges(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length != 3 || changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Character) != 1 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Items) != 1 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Mailbox) != 1)
            throw new ArgumentException("A mailbox transaction requires one Character, Items and Mailbox target.");
        foreach (var change in changes)
            if (change.FileId <= 0 || change.Before is null || change.After is null ||
                change.Before.Length > MaxTargetBytes || change.After.Length > MaxTargetBytes)
                throw new ArgumentException("Invalid required mailbox transaction image or identity.");
        if (changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Items).FileId !=
            changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Mailbox).FileId)
            throw new ArgumentException("Item and mailbox character IDs must match.");
    }
}
