using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryPrepareNoAllocationWork(LegacyMailboxPackageRecipe recipe,
        IReadOnlyList<LegacyTradeJournalChange> changes, out PreparedJointWork? work, out bool admitted, out string reason)
    {
        work = null;
        admitted = false;
        reason = "mailboxClaimPackageNoAllocationInvalid";
        try
        {
            if (recipe is null || changes is null || recipe.Count != 0 || !IsCurrent(UsersRoot, ItemsRoot, MailboxRoot, Recovery)) return false;
            if (!workStore.CanPrepare(recipe, out var priorAdmission, out reason))
            { admitted |= priorAdmission; return false; }
            admitted |= priorAdmission;
            if (workStore.HasOtherRetainedLineage(recipe))
            { admitted = true; reason = "mailboxClaimRecoveryRequired:packagePriorWork"; return false; }
            if (!Registry.TryReadActiveProducerIntents(Domain, out var intents, out reason)) return false;
            foreach (var intent in intents.Where(intent => intent.Request.Writer == Writer &&
                intent.Request.AccountId == recipe.AccountId && intent.Request.CharacterUid == recipe.CharacterUid))
            {
                if (!LegacyMailboxPackageRecipe.TryReadHeader(intent.CopyPayload(), Domain, recipe.AccountId,
                    recipe.CharacterUid, out var keys) || recipe.Overlaps(keys))
                { admitted = true; reason = "mailboxClaimRecoveryRequired:packagePriorAllocatingIntent"; return false; }
            }
            if (!TryCopyWorkImages(changes, out var copies))
            { reason = "mailboxClaimRecoveryRequired:packageWorkImagesInvalid"; return false; }
            var paths = WorkPaths(recipe.AccountId, recipe.CharacterUid);
            PreparedJointWork? prepared = null;
            // Metadata publication may have succeeded even if its observation fails.
            // All later failures retain/fence that possible durable work admission.
            admitted = true;
            SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                var row = workStore.PrepareNoAllocationWork(recipe, copies);
                prepared = PreparedJointWork.FromPersistedWork(this, row, copies, paths);
                RetainSessionPublication(recipe.AccountId, recipe.OperationId);
                return true;
            });
            work = prepared;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:packageNoAllocationWork:" + error.GetType().Name; return false; }
    }
}
