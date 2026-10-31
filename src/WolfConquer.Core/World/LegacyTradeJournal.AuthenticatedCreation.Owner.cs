using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    [ThreadStatic] private static LegacyAuthenticatedOrdinaryCreationProducer? activeAuthenticatedCreation;

    internal static bool TryCommitAuthenticatedOrdinaryCreation(LegacyAuthenticatedOrdinaryCreationProducer producer,
        IReadOnlyList<LegacyTradeJournalChange> changes, LegacyJournalInterruption? interruption, out string reason)
    {
        reason = "authenticatedCreationJournalInvalid";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            if (activeAuthenticatedCreation is not null)
                throw new InvalidDataException("Authenticated creation journal scope is already occupied.");
            var recipe = producer.Recipe;
            var roots = GetAuthenticatedCreationRoots(recipe.Roots, recipe.MailboxRoot);
            var operation = producer.CopyOperation();
            var record = CreateRecord(changes, 19) with { AdmissionDomain = recipe.Roots,
                CreationMailbox = recipe.MailboxRoot, OrdinaryCreation = operation };
            _ = ValidateAuthenticatedCreationImages(record, uint.MaxValue);
            var targets = GetTargets(roots, record);
            var paths = AuthenticatedCreationPaths(recipe.Roots, recipe.MailboxRoot, recipe.Account);
            var failure = string.Empty;
            var committed = SnapshotFileLockCoordinator.ExecuteLocked(paths.Values.ToArray(), () =>
            {
                producer.BeginJournal(record.Changes, record.TransactionId, operation);
                activeAuthenticatedCreation = producer;
                try { return CommitLocked(roots, record, targets, interruption, out failure); }
                finally { activeAuthenticatedCreation = null; }
            });
            reason = failure; return committed;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = "authenticatedCreationJournalInvalid:" + error.GetType().Name; return false; }
    }

    private static void RequireAuthenticatedCreationMutation(JournalRecord record, JournalTarget[] targets)
    {
        var producer = activeAuthenticatedCreation ??
            throw new InvalidDataException("Creation journal lacks its actual authenticated producer.");
        RequireAuthenticatedCreationBoundary(record);
        LegacyMailboxPackageMutationFence.RequireAuthenticatedCreationJournalMutation(producer,
            targets.Select(target => target.Path).ToArray(), record.Changes);
    }

    private static void RequireAuthenticatedCreationBoundary(JournalRecord record)
    {
        if (record.Version != 19) return;
        var producer = activeAuthenticatedCreation ??
            throw new InvalidDataException("Creation write boundary lacks its actual authenticated producer.");
        producer.RequireJournalBoundary(record.Changes, record.TransactionId, record.OrdinaryCreation!);
        var roots = GetAuthenticatedCreationRoots(record.AdmissionDomain!, record.CreationMailbox!);
        EnsureSafePath(roots.Journal);
        if (!Directory.Exists(roots.Journal)) return;
        var entries = Directory.GetFileSystemEntries(roots.Journal);
        if (entries.Length == 0) return;
        var pending = Path.Combine(roots.Journal, PendingName);
        var preparing = Path.Combine(roots.Journal, record.TransactionId + ".preparing");
        if (entries.Length != 1 || (!JournalDomainComparer.Equals(entries[0], pending) &&
                !JournalDomainComparer.Equals(entries[0], preparing)) ||
            !ReadRecordImage(entries[0]).AsSpan().SequenceEqual(Serialize(record)))
            throw new InvalidDataException("Authenticated creation pending/preparing identity changed.");
    }
}
