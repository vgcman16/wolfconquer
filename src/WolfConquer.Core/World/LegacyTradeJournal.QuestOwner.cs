using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitQuestOwner(string usersRoot, string itemsRoot, string questsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, out string reason) =>
        TryCommitQuestOwner(usersRoot, itemsRoot, questsRoot, changes, null, out reason);

    // Version 3: one unchanged actual account image, plus one character's item
    // and quest images. This is not quest eligibility or account-UID validation.
    // Failure-only boundaries: 0 durable record, 1 Character, 2 Items, 3 Quests.
    internal static bool TryCommitQuestOwner(string usersRoot, string itemsRoot, string questsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes,
        Func<int, bool>? interruptAfterReplacement, out string reason)
    {
        reason = string.Empty;
        try
        {
            var roots = GetQuestRoots(usersRoot, itemsRoot, questsRoot);
            var record = CreateRecord(changes, version: 3) with
            {
                Domain = new(roots.Users, roots.Items, roots.Quests!),
            };
            var targets = GetTargets(roots, record);
            var lockedReason = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(),
                () => CommitLocked(roots, record, targets, interruptAfterReplacement, out lockedReason));
            reason = lockedReason;
            return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        {
            reason = "tradeJournalInputInvalid:" + error.GetType().Name;
            return false;
        }
    }

    private static void ValidateQuestChanges(LegacyTradeJournalChange[] changes)
    {
        if (changes.Length != 3 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Character) != 1 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Items) != 1 ||
            changes.Count(change => change.Kind == LegacyTradeJournalFileKind.Quests) != 1)
            throw new ArgumentException("A quest transaction requires exactly one target of each kind.");
        foreach (var change in changes)
            if (change.FileId <= 0 || change.Before is null || change.After is null ||
                change.Before.Length > MaxTargetBytes || change.After.Length > MaxTargetBytes)
                throw new ArgumentException("Invalid required quest-transaction identity or image.");
        var character = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Character);
        var items = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Items);
        var quests = changes.Single(change => change.Kind == LegacyTradeJournalFileKind.Quests);
        if (!character.Before!.AsSpan().SequenceEqual(character.After) || items.FileId != quests.FileId)
            throw new ArgumentException("The account must be a no-op and item/quest character IDs must match.");
    }
}
