using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitPairMovement(string usersRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyPreparedPairMovementWrite.JournalOwner producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "pairMovementJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            ArgumentNullException.ThrowIfNull(changes);
            if (changes.GetType() != typeof(LegacyTradeJournalChange[]) &&
                changes.GetType() != typeof(List<LegacyTradeJournalChange>))
                throw new ArgumentException("Pair movement targets require a closed captured list.");
            var roots = GetCharacterResourcesRoots(usersRoot);
            var operation = producer.Operation;
            var record = CreateRecord(changes, 21) with { PairMovement = operation, PairMovementOwner = producer };
            RequirePairMovementProducer(producer, roots.Users, record.Changes, operation);
            ValidateRecordedPairMovementImages(record);
            var targets = GetTargets(roots, record); var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(targets.Select(target => target.Path).ToArray(), () =>
            {
                RequirePairMovementBoundary(roots, record);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "pairMovementJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void RequirePairMovementProducer(LegacyPreparedPairMovementWrite.JournalOwner producer,
        string usersRoot, IReadOnlyList<LegacyTradeJournalChange> changes, LegacyPairMovementOperation operation)
    {
        try
        {
            if (!ReferenceEquals(producer.Operation, operation))
                throw new InvalidDataException("Pair movement operation differs from its actual producer.");
            operation.Validate();
            producer.RequireJournalTargets(usersRoot, changes);
            if (!producer.IsCurrent()) throw new InvalidDataException("Pair movement participants are no longer current.");
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Pair movement lacks its actual current producer and targets.", error); }
    }

    private static void RequirePairMovementBoundary(JournalRoots roots, JournalRecord record)
    {
        if (record.Version != 21) return;
        var producer = record.PairMovementOwner ??
            throw new InvalidDataException("Pair movement boundary lacks its actual prepared writer.");
        RequirePairMovementProducer(producer, roots.Users, record.Changes, record.PairMovement!);
        EnsureTargetsMatch(GetPairMovementTargets(roots, record), allowAfter: true);
        EnsureSafePath(roots.Journal);
        if (!Directory.Exists(roots.Journal)) return;
        var entries = Directory.GetFileSystemEntries(roots.Journal);
        if (entries.Length == 0) return;
        var pending = Path.Combine(roots.Journal, PendingName);
        var preparing = Path.Combine(roots.Journal, record.TransactionId + ".preparing");
        if (entries.Length != 1 || (!JournalDomainComparer.Equals(entries[0], pending) &&
                !JournalDomainComparer.Equals(entries[0], preparing)) ||
            !ReadRecordImage(entries[0]).AsSpan().SequenceEqual(Serialize(record)))
            throw new InvalidDataException("Pair movement pending/preparing record changed.");
    }
}
