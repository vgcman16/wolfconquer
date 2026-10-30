namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitRuneOperation(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRuneOperation operation, LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "runeOperationJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(operation); operation.Validate();
            var frozen = operation with { Metadata = operation.Metadata.ToArray() };
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 14) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RuneOperation = frozen };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = WolfConquer.Core.Data.SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                ValidateRecordedRuneImages(record, uint.MaxValue);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "runeOperationJournalInvalid:" + error.GetType().Name; return false; }
    }
}
