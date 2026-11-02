using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitRelicDecompose(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRelicDecomposeOwnedEvidence producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "relicDecomposeJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            producer.RequireJournalTargets(usersRoot, itemsRoot, spellsRoot, changes);
            if (!producer.TryCreateJournalData(out var operation) || operation is null)
                throw new InvalidDataException("relicDecomposeJournalAllocationUnavailable");
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 27) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RelicDecompose = operation };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                producer.RequireJournalTargets(roots.Users, roots.Items, roots.Spells!, record.Changes);
                ValidateRelicDecomposeImages(record.Changes, operation,
                    checked(operation.Allocation.First + (uint)operation.Allocation.Count - 1));
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "relicDecomposeJournalInvalid:" + error.GetType().Name; return false; }
    }
}
