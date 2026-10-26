using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    // Inspection never allocates or pays. It makes a failed re-admission truthful
    // when an earlier process has already durably admitted the same mail lineage.
    internal bool TryInspectRecipeHistory(uint account, uint uid, LegacyMailboxEntry[] selected,
        out bool admitted, out string reason)
    {
        admitted = false;
        try
        {
            if (!Registry.TryReadActiveProducerIntents(Domain, out var intents, out reason))
            { admitted = true; reason = "mailboxClaimRecoveryRequired:packageHistoryUnproven:" + reason; return false; }
            var keys = LegacyMailboxPackageRecipe.CaptureLineageKeys(selected);
            if (workStore.HasCompletedLineage(account, uid, keys)) admitted = true;
            foreach (var row in intents.Where(row => row.Request.Writer == Writer && row.Request.AccountId == account &&
                row.Request.CharacterUid == uid))
            {
                if (!LegacyMailboxPackageRecipe.TryReadHeader(row.CopyPayload(), Domain, account, uid, out var recorded))
                { admitted = true; reason = "mailboxPackageRecipeHistoryUnproven"; return false; }
                if (recorded.Intersect(keys, StringComparer.Ordinal).Any()) admitted = true;
            }
            foreach (var row in workStore.ReadPendingNoAllocationRecords().Where(row => row.AccountId == account && row.CharacterUid == uid))
            {
                if (!LegacyMailboxPackageRecipe.TryReadHeader(Convert.FromBase64String(row.PayloadBase64!), Domain, account, uid, out var recorded))
                { admitted = true; reason = "mailboxPackageRecipeHistoryUnproven"; return false; }
                if (recorded.Intersect(keys, StringComparer.Ordinal).Any()) admitted = true;
            }
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { admitted = true; reason = "mailboxClaimRecoveryRequired:packageHistoryRead:" + error.GetType().Name; return false; }
    }
}
