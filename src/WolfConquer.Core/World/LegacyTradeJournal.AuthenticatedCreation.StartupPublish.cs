using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryCompleteAuthenticatedCreationStartup(LegacyItemUidRegistry.OwnedStartupCustody custody,
        LegacyAuthenticatedOrdinaryCreationOperation operation, out string reason)
    {
        reason = "authenticatedCreationRecordedStartupUnproven";
        if (RefuseJournalReentry(ref reason)) return false;
        try
        {
            custody.RequireAuthenticatedCreationAllocation(operation);
            var configured = custody.Configuration;
            var recipe = LegacyAuthenticatedOrdinaryCreationRecipe.Decode(operation.Payload, configured.Roots, configured.MailboxRoot);
            var roots = GetAuthenticatedCreationRoots(configured.Roots, configured.MailboxRoot);
            var after = recipe.AfterImages(operation.Allocation);
            var changes = LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds.Select(kind =>
                new LegacyTradeJournalChange(kind, recipe.Account, null, after[kind]!)).ToArray();
            var record = CreateRecord(changes, 19) with { AdmissionDomain = configured.Roots,
                CreationMailbox = configured.MailboxRoot, OrdinaryCreation = operation with { Payload = operation.Payload.ToArray() } };
            _ = ValidateAuthenticatedCreationImages(record, custody.HighWater);
            var targets = GetTargets(roots, record);
            var names = LegacyCharacterCreationRecoveryEvidence.Capture(configured, recipe.Account,
                recipe.Projection.Name, record.Changes, record.TransactionId);
            RequireClearRuneDecomposeNamespace(configured);
            if (!LegacyItemUidRawCatalog.TryScanAuthenticatedCreationStartup(configured.ItemsRoot, configured.MailboxRoot,
                    configured.RedeemFile, configured.ClaimFile, custody, operation, out var observed, out reason)) return false;
            if (observed.Maximum > custody.HighWater)
            { reason = RecoveryRequired + ":authenticatedCreationHighWaterExceeded"; return false; }
            var failure = string.Empty;
            if (!SnapshotFileLockCoordinator.ExecuteLocked(AdmissionLockPaths(roots, record, targets),
                    () => PublishAuthenticatedCreationStartup(custody, roots, record, targets, names, out failure)))
            { reason = failure; return false; }
            // The actual pending bytes now exist. Use the ordinary closed
            // captured-record/custody scan, staging and replacement pipeline.
            if (!TryRecoverOwnedStartupItems(custody, out reason)) return false;
            custody.RequireCurrent();
            return true;
        }
        catch (Exception error) when (IsJournalException(error))
        { reason = RecoveryRequired + ":authenticatedCreationStartup:" + error.GetType().Name; return false; }
    }

    private static bool PublishAuthenticatedCreationStartup(LegacyItemUidRegistry.OwnedStartupCustody custody,
        JournalRoots roots, JournalRecord record, JournalTarget[] targets,
        LegacyCharacterCreationRecoveryEvidence names, out string reason)
    {
        reason = string.Empty;
        var preparing = Path.Combine(roots.Journal, record.TransactionId + ".preparing");
        var pending = Path.Combine(roots.Journal, PendingName);
        var published = false;
        using var serialized = new JournalDomainScope(roots.Users, mutation: true);
        try
        {
            custody.RequireCurrent(); names.RequireCurrent();
            RequireClearRuneDecomposeNamespace(custody.Configuration);
            EnsureTargetsMatch(targets, allowAfter: false); EnsureAuthenticatedCreationInputs(roots, record);
            Directory.CreateDirectory(roots.Journal); EnsureSafePath(roots.Journal);
            WriteFlushed(preparing, Serialize(record));
            custody.RequireCurrent(); names.RequireCurrent();
            EnsureTargetsMatch(targets, allowAfter: false); EnsureAuthenticatedCreationInputs(roots, record);
            var entries = Directory.GetFileSystemEntries(roots.Journal);
            if (entries.Length != 1 || !JournalDomainComparer.Equals(entries[0], preparing) ||
                !ReadRecordImage(preparing).AsSpan().SequenceEqual(Serialize(record)))
                throw new InvalidDataException("Recorded creation preparing identity changed.");
            File.Move(preparing, pending); published = true;
            return true;
        }
        catch (Exception error) when (IsJournalException(error))
        {
            reason = RecoveryRequired + ":authenticatedCreationPublish:" + error.GetType().Name;
            return false;
        }
        finally
        {
            if (!published && !TryDelete(preparing))
                reason += ";preparingCleanupIncomplete";
        }
    }
}
