using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Typed file prerequisite only: no UID producer, gameplay intent, Ninja owner or Host binding.
    internal static bool TryCommitMailboxGearOwner(string usersRoot, string itemsRoot, string mailboxRoot,
        string combatGearRoot, IReadOnlyList<LegacyTradeJournalChange> changes, out string reason) =>
        TryCommitMailboxGearOwner(usersRoot, itemsRoot, mailboxRoot, combatGearRoot, changes, null, out reason);

    // Failure-only boundaries: 0 pending published, then Character, Items, Mailbox, CombatGear.
    internal static bool TryCommitMailboxGearOwner(string usersRoot, string itemsRoot, string mailboxRoot,
        string combatGearRoot, IReadOnlyList<LegacyTradeJournalChange> changes, Func<int, bool>? interrupt, out string reason)
    {
        reason = string.Empty;
        try
        {
            var roots = GetMailboxGearRoots(usersRoot, itemsRoot, mailboxRoot, combatGearRoot);
            var record = CreateRecord(changes, 5) with
            { MailboxGearDomain = new(roots.Users, roots.Items, roots.Mailbox!, roots.CombatGear!) };
            var targets = GetTargets(roots, record);
            var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(),
                () => CommitLocked(roots, record, targets, interrupt, out failure));
            reason = failure;
            return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "tradeJournalInputInvalid:" + error.GetType().Name; return false; }
    }

    private static void ValidateMailboxGearChanges(LegacyTradeJournalChange[] changes)
    {
        LegacyTradeJournalFileKind[] kinds = [LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items,
            LegacyTradeJournalFileKind.Mailbox, LegacyTradeJournalFileKind.CombatGear];
        if (changes.Length != kinds.Length || kinds.Any(kind => changes.Count(change => change.Kind == kind) != 1))
            throw new ArgumentException("A mailbox/gear transaction requires exactly one target of each kind.");
        foreach (var change in changes)
            if (change.FileId <= 0 || change.Before is null || change.After is null ||
                change.Before.Length > MaxTargetBytes || change.After.Length > MaxTargetBytes)
                throw new ArgumentException("Invalid required mailbox/gear image or identity.");
        var uid = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Items).FileId;
        if (changes.Any(change => change.Kind != LegacyTradeJournalFileKind.Character && change.FileId != uid))
            throw new ArgumentException("Item, mailbox and CombatGear character IDs must match.");
        var gear = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.CombatGear);
        ValidateMailboxGearImage(gear.Before!);
        ValidateMailboxGearImage(gear.After);
    }
}
