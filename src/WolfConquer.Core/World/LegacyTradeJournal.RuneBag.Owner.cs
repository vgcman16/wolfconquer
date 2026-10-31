using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitRuneBag(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRuneBagEvidence producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "runeBagJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            RequireRuneBagProducer(producer, usersRoot, itemsRoot, spellsRoot, changes);
            var operation = producer.Operation; operation.Validate();
            var frozen = operation with { Metadata = operation.Metadata with { } };
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 20) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RuneBag = frozen };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                RequireRuneBagProducer(producer, roots.Users, roots.Items, roots.Spells!, record.Changes);
                ValidateRecordedRuneBagImages(record, uint.MaxValue);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "runeBagJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void RequireRuneBagProducer(LegacyRuneBagEvidence producer,
        string usersRoot, string itemsRoot, string spellsRoot, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        try
        {
            producer.RequireJournalTargets(usersRoot, itemsRoot, spellsRoot, changes);
            if (!producer.IsCurrent()) throw new InvalidDataException("Direct RuneBag producer is no longer current.");
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Direct RuneBag lacks its actual current producer and targets.", error); }
    }
}
