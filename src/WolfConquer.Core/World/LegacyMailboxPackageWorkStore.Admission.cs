namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    internal bool CanPrepare(LegacyMailboxPackageRecipe recipe, out bool admitted, out string reason)
    {
        admitted = false;
        reason = "mailboxClaimPackageWorkLedgerFull";
        EnsureHistoryLayout();
        var index = ReadAdmittedIndex();
        var pending = index.Pending;
        var current = Read(recipe.OperationId);
        if (current is null)
        {
            var available = pending.Length < MaximumPendingRecords;
            if (available) reason = string.Empty;
            return available;
        }
        admitted = true;
        reason = "mailboxClaimRecoveryRequired:packageWorkRecipeConflict";
        var digest = Digest(recipe.CopyPayload());
        if (current.Completed || current.PayloadSha256 != digest || current.Count != recipe.Count ||
            (recipe.Count == 0 ? current.Version != 2 : current.Version != 1)) return false;
        reason = string.Empty;
        return true;
    }

    internal bool HasOtherRetainedLineage(LegacyMailboxPackageRecipe recipe)
    {
        if (!LegacyMailboxPackageRecipe.TryReadHeader(recipe.CopyPayload(), domain, recipe.AccountId,
            recipe.CharacterUid, out var keys)) throw new InvalidDataException("Package lineage is invalid.");
        if (HasCompletedLineage(recipe.AccountId, recipe.CharacterUid, keys)) return true;
        return ReadAdmittedIndex().Pending.Any(row => row.Operation != recipe.OperationId.ToString("N") &&
            row.AccountId == recipe.AccountId && row.CharacterUid == recipe.CharacterUid &&
            HistoryLineages(row).Intersect(keys, StringComparer.Ordinal).Any());
    }
}
