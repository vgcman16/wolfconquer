using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitPhysicalDeath(string usersRoot, string itemsRoot, string spellsRoot,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyPreparedBasicPhysicalWrite.JournalOwner producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "physicalDeathJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer);
            ArgumentNullException.ThrowIfNull(changes);
            if (changes.GetType() != typeof(LegacyTradeJournalChange[]) &&
                changes.GetType() != typeof(List<LegacyTradeJournalChange>))
                throw new ArgumentException("Physical death requires a closed captured target list.");
            var roots = GetCharacterItemsSpellsRoots(usersRoot, itemsRoot, spellsRoot);
            var operation = producer.Operation;
            // CreateRecord deep-copies complete caller bytes before consulting
            // the concrete producer. A caller list never becomes permission.
            var record = CreateRecord(changes, 22) with
            {
                CharacterItemsSpellsDomain = new(roots.Users, roots.Items, roots.Spells!),
                PhysicalDeath = operation,
                PhysicalDeathOwner = producer,
            };
            RequirePhysicalDeathProducer(roots, record);
            ValidateRecordedPhysicalDeathImages(record);
            var targets = GetPhysicalDeathTargets(roots, record);
            var paths = targets.Select(target => target.Path).Append(PhysicalDeathItemsPath(roots, record)).ToArray();
            var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                RequirePhysicalDeathBoundary(roots, record);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure;
            return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "physicalDeathJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void RequirePhysicalDeathProducer(JournalRoots roots, JournalRecord record)
    {
        var producer = record.PhysicalDeathOwner ??
            throw new InvalidDataException("Physical death lacks its actual prepared writer.");
        var operation = record.PhysicalDeath ?? throw new InvalidDataException("Missing physical death operation.");
        if (roots.Spells is null || !ReferenceEquals(producer.Operation, operation))
            throw new InvalidDataException("Physical death operation differs from its actual producer.");
        try
        {
            operation.Validate();
            producer.RequireJournalTargets(roots.Users, roots.Items, roots.Spells, record.Changes);
            if (!producer.IsCurrent()) throw new InvalidDataException("Physical death producer is no longer current.");
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Physical death lacks its actual current producer and images.", error); }
    }

    private static void RequirePhysicalDeathBoundary(JournalRoots roots, JournalRecord record)
    {
        if (record.Version != 22) return;
        RequirePhysicalDeathProducer(roots, record);
        RequirePhysicalDeathItemsCurrent(roots, record);
        EnsureTargetsMatch(GetPhysicalDeathTargets(roots, record), allowAfter: true);
        EnsureSafePath(roots.Journal);
        if (!Directory.Exists(roots.Journal)) return;
        var entries = Directory.GetFileSystemEntries(roots.Journal);
        if (entries.Length == 0) return;
        var pending = Path.Combine(roots.Journal, PendingName);
        var preparing = Path.Combine(roots.Journal, record.TransactionId + ".preparing");
        if (entries.Length != 1 || (!JournalDomainComparer.Equals(entries[0], pending) &&
                !JournalDomainComparer.Equals(entries[0], preparing)) ||
            !ReadRecordImage(entries[0]).AsSpan().SequenceEqual(Serialize(record)))
            throw new InvalidDataException("Physical death pending/preparing record changed.");
    }
}
