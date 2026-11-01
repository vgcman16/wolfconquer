using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCommitPhysicalHitProgression(string users, string items, string spells, string proficiencies,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyPreparedBasicPhysicalHitProgressionWrite.JournalOwner producer,
        LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "physicalHitProgressionJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            ArgumentNullException.ThrowIfNull(producer); ArgumentNullException.ThrowIfNull(changes);
            if (changes.GetType() != typeof(LegacyTradeJournalChange[]) && changes.GetType() != typeof(List<LegacyTradeJournalChange>))
                throw new ArgumentException("Hit progression requires a closed captured list.");
            var roots = GetHitProgressionRoots(users, items, spells, proficiencies);
            var domain = new HitProgressionJournalDomain(roots.Users, roots.Items, roots.Spells!, roots.Proficiencies!);
            RequireHitProgressionEnvelopeBudget(changes, producer.Operation, domain); // Before any record clone.
            var record = CreateRecord(changes, 24) with
            { HitProgressionDomain = domain, PhysicalHitProgression = producer.Operation, PhysicalHitProgressionOwner = producer };
            RequireHitProgressionProducer(roots, record);
            var targets = GetHitProgressionTargets(roots, record);
            var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(AdmissionLockPaths(roots, record, targets), () =>
            {
                RequireHitProgressionBoundary(roots, record);
                return CommitLocked(roots, record, targets, interruption, out failure);
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "physicalHitProgressionJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void RequireHitProgressionProducer(JournalRoots roots, JournalRecord record)
    {
        var producer = record.PhysicalHitProgressionOwner;
        if (producer is null || !ReferenceEquals(producer.Operation, record.PhysicalHitProgression) ||
            roots.Spells is null || roots.Proficiencies is null) throw new InvalidDataException("Missing actual hit progression producer.");
        try
        {
            producer.RequireJournalTargets(roots.Users, roots.Items, roots.Spells, roots.Proficiencies, record.Changes);
            if (!producer.IsCurrent()) throw new InvalidDataException("Hit progression producer is no longer current.");
        }
        catch (InvalidOperationException error) { throw new InvalidDataException("Hit progression producer changed.", error); }
    }

    private static void RequireHitProgressionBoundary(JournalRoots roots, JournalRecord record)
    {
        if (record.Version != 24) return;
        RequireHitProgressionProducer(roots, record);
        RequireHitProgressionReferencesCurrent(roots, record);
        var targets = GetHitProgressionTargets(roots, record);
        RequireHitProgressionPrefix(targets);
        RequireHitProgressionStageDomain(roots, record, targets);
        EnsureSafePath(roots.Journal);
        if (!Directory.Exists(roots.Journal)) return;
        var entries = Directory.GetFileSystemEntries(roots.Journal);
        if (entries.Length == 0) return;
        var pending = Path.Combine(roots.Journal, PendingName);
        var preparing = Path.Combine(roots.Journal, record.TransactionId + ".preparing");
        if (entries.Length != 1 || !JournalDomainComparer.Equals(entries[0], pending) && !JournalDomainComparer.Equals(entries[0], preparing) ||
            !ReadRecordImage(entries[0]).AsSpan().SequenceEqual(Serialize(record)))
            throw new InvalidDataException("Hit progression pending/preparing record changed.");
    }
}
