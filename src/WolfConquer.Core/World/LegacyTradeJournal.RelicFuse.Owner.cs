using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // The producer must be the real retained before-image/UID owner. The
    // record is replayed only through actual owned startup custody.
    internal static bool TryCommitRelicFuse(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRelicFuseOwnedEvidence producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "relicFuseJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            producer.RequireJournalTargets(usersRoot, itemsRoot, spellsRoot, changes);
            if (!producer.TryCreateJournalData(out var operation) || operation is null)
                throw new InvalidDataException("relicFuseJournalAllocationUnavailable");
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 25) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RelicFuse = operation };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                producer.RequireJournalTargets(roots.Users, roots.Items, roots.Spells!, record.Changes);
                ValidateRelicFuseImages(record.Changes, operation, operation.Allocation.First);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "relicFuseJournalInvalid:" + error.GetType().Name; return false; }
    }
}
