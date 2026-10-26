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
        if (!Registry.TryReadProducerIntents(Domain, out var intents, out reason))
        { admitted = reason.StartsWith(LegacyItemUidRegistry.RecoveryRequired, StringComparison.Ordinal); return false; }
        var keys = LegacyMailboxPackageRecipe.CaptureLineageKeys(selected);
        foreach (var row in intents.Where(row => row.Request.Writer == Writer && row.Request.AccountId == account &&
            row.Request.CharacterUid == uid))
        {
            if (!LegacyMailboxPackageRecipe.TryReadHeader(row.CopyPayload(), Domain, account, uid, out var recorded))
            { admitted = true; reason = "mailboxPackageRecipeHistoryUnproven"; return false; }
            if (recorded.Intersect(keys, StringComparer.Ordinal).Any()) admitted = true;
        }
        reason = string.Empty;
        return true;
    }
}
