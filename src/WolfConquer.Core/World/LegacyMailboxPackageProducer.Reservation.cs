using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    // The caller has already validated the real owner, eligibility, complete
    // before-images and deterministic grant recipe. This is durable work admission,
    // not a completed reward. All later faults must retain/fence this work.
    internal bool TryReserveRecipe(LegacyMailboxPackageRecipe recipe, out LegacyItemUidReservationReceipt? receipt,
        out bool admitted, out string reason)
    {
        receipt = null; admitted = false;
        reason = "mailboxPackageRecipeInvalid";
        if (recipe is null) return false;
        try
        {
            if (!workStore.CanPrepare(recipe, out var workAdmitted, out reason))
            { admitted |= workAdmitted; return false; }
            admitted |= workAdmitted;
            if (workStore.HasOtherRetainedLineage(recipe))
            { admitted = true; reason = "mailboxPackageRecipeOverlap"; return false; }
        }
        catch (Exception error) when (IsWorkFailure(error))
        { admitted = true; reason = "mailboxClaimRecoveryRequired:packageWorkAdmission:" + error.GetType().Name; return false; }
        var payload = recipe.CopyPayload();
        var operationId = recipe.OperationId; var accountId = recipe.AccountId; var characterUid = recipe.CharacterUid; var count = recipe.Count;
        if (payload is null || payload.Length is < 1 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes ||
            operationId == Guid.Empty || accountId == 0 || characterUid == 0 ||
            count is < 1 or > LegacyItemUidRegistry.MaximumReservation) return false;
        var copied = payload.ToArray();
        var digest = Convert.ToHexString(SHA256.HashData(copied)).ToLowerInvariant();
        var request = new LegacyItemUidReservationRequest(operationId, Writer, accountId, characterUid, digest, count);
        if (!TryRelieveCompletedIntentPressure(copied.Length, out reason)) return false;
        if (!Registry.TryReadActiveProducerIntents(Domain, out var existing, out reason))
        { admitted |= reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal); return false; }
        foreach (var row in existing.Where(row => row.Request.Writer == Writer && row.Request.AccountId == accountId && row.Request.CharacterUid == characterUid))
        {
            if (!LegacyMailboxPackageRecipe.TryReadHeader(row.CopyPayload(), Domain, accountId, characterUid, out var keys))
            { reason = "mailboxPackageRecipeHistoryUnproven"; admitted = true; return false; }
            if (row.Request.OperationId != operationId && recipe.Overlaps(keys))
            { reason = "mailboxPackageRecipeOverlap"; admitted = true; return false; }
            if (row.Request.OperationId == operationId) admitted = true;
        }
        if (!Registry.TryPrepareProducerIntent(Domain, request, copied, out var prepared, out reason))
        {
            admitted |= reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal);
            return false;
        }
        admitted = true;
        if (prepared is null) return false;
        var allocated = Registry.TryReserveProducerIntent(Domain, request, copied, out var reserved, out reason);
        if (!allocated && reason == "itemUidOperationLedgerFull" && TryRelieveCompletedIntentPressure(0, out reason, force: true))
            allocated = Registry.TryReserveProducerIntent(Domain, request, copied, out reserved, out reason);
        if (!allocated) return false;
        receipt = reserved?.Allocation;
        if (receipt is null || receipt.DomainId != Domain || receipt.OperationId != operationId ||
            receipt.Writer != Writer || receipt.AccountId != accountId || receipt.CharacterUid != characterUid ||
            receipt.PayloadSha256 != digest || receipt.Count != count || receipt.First == 0 ||
            (ulong)receipt.First + (uint)count - 1 > uint.MaxValue)
        { reason = "mailboxPackageReceiptUnproven"; receipt = null; return false; }
        reason = string.Empty;
        return true;
    }
}
