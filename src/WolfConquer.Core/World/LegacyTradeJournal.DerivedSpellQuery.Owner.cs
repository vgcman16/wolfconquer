using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitDerivedSpellQuery(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyPreparedDerivedSpellQueryWrite.JournalOwner producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "derivedSpellQueryJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            ArgumentNullException.ThrowIfNull(changes);
            if (changes.GetType() != typeof(LegacyTradeJournalChange[]) &&
                changes.GetType() != typeof(List<LegacyTradeJournalChange>))
                throw new ArgumentException("Derived spell query requires a closed captured target list.");
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var operation = producer.Operation;
            // CreateRecord deep-copies complete caller bytes before consulting
            // the concrete producer. A caller list never becomes permission.
            var record = CreateRecord(changes, 23) with
            {
                CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!),
                DerivedSpellQuery = operation,
                DerivedSpellQueryOwner = producer,
            };
            RequireDerivedSpellQueryProducer(roots, record);
            ValidateRecordedDerivedSpellQueryImages(record);
            var targets = GetDerivedSpellQueryTargets(roots, record);
            var paths = targets.Select(target => target.Path).Append(DerivedSpellQueryItemsPath(roots, record)).ToArray();
            var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                RequireDerivedSpellQueryBoundary(roots, record);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure;
            return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "derivedSpellQueryJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void RequireDerivedSpellQueryProducer(JournalRoots roots, JournalRecord record)
    {
        var producer = record.DerivedSpellQueryOwner ??
            throw new InvalidDataException("Derived spell query lacks its actual prepared writer.");
        var operation = record.DerivedSpellQuery ?? throw new InvalidDataException("Missing derived spell query operation.");
        if (roots.Spells is null || !ReferenceEquals(producer.Operation, operation))
            throw new InvalidDataException("Derived spell query operation differs from its actual producer.");
        try
        {
            operation.Validate();
            producer.RequireJournalTargets(roots.Users, roots.Items, roots.Spells, record.Changes);
            if (!producer.IsCurrent()) throw new InvalidDataException("Derived spell query producer is no longer current.");
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Derived spell query lacks its actual current producer and images.", error); }
    }

    private static void RequireDerivedSpellQueryBoundary(JournalRoots roots, JournalRecord record)
    {
        if (record.Version != 23) return;
        RequireDerivedSpellQueryProducer(roots, record);
        RequireDerivedSpellQueryItemsCurrent(roots, record);
        EnsureTargetsMatch(GetDerivedSpellQueryTargets(roots, record), allowAfter: true);
        EnsureSafePath(roots.Journal);
        if (!Directory.Exists(roots.Journal)) return;
        var entries = Directory.GetFileSystemEntries(roots.Journal);
        if (entries.Length == 0) return;
        var pending = Path.Combine(roots.Journal, PendingName);
        var preparing = Path.Combine(roots.Journal, record.TransactionId + ".preparing");
        if (entries.Length != 1 || (!JournalDomainComparer.Equals(entries[0], pending) &&
                !JournalDomainComparer.Equals(entries[0], preparing)) ||
            !ReadRecordImage(entries[0]).AsSpan().SequenceEqual(Serialize(record)))
            throw new InvalidDataException("Derived spell query pending/preparing record changed.");
    }
}
