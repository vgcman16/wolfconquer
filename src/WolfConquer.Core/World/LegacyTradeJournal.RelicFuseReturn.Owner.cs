using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // This fixed writer requires the real return owner; replay requires owned
    // startup custody and validates the original existing physical UID.
    internal static bool TryCommitRelicFuseReturn(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRelicFuseReturnOwnedEvidence producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "relicFuseReturnJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            producer.RequireJournalTargets(usersRoot, itemsRoot, spellsRoot, changes);
            var operation = producer.CreateJournalData();
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 26) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RelicFuseReturn = operation };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                producer.RequireJournalTargets(roots.Users, roots.Items, roots.Spells!, record.Changes);
                ValidateRelicFuseReturnImages(record.Changes, operation, producer.HighWater);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "relicFuseReturnJournalInvalid:" + error.GetType().Name; return false; }
    }
}
