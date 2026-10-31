using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitRuneDecompose(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyRuneDecomposeEvidence producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "runeDecomposeJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            RequireRuneDecomposeProducer(producer, usersRoot, itemsRoot, spellsRoot, changes);
            var operation = producer.Operation; operation.Validate();
            var frozen = operation with { RuneMetadata = operation.RuneMetadata.ToArray(),
                RewardMetadata = operation.RewardMetadata.ToArray() };
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var record = CreateRecord(changes, 18) with
            { CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!), RuneDecompose = frozen };
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                RequireRuneDecomposeProducer(producer, roots.Users, roots.Items, roots.Spells!, record.Changes);
                ValidateRecordedRuneDecomposeImages(record, uint.MaxValue);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "runeDecomposeJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void RequireRuneDecomposeProducer(LegacyRuneDecomposeEvidence producer,
        string usersRoot, string itemsRoot, string spellsRoot, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        try
        {
            producer.RequireJournalTargets(usersRoot, itemsRoot, spellsRoot, changes);
            if (!producer.IsCurrent()) throw new InvalidDataException("Rune decomposition producer is no longer current.");
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Rune decomposition lacks its actual current producer and targets.", error); }
    }
}
