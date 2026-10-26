using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Legacy internal failure ABI only; this owner no longer admits new grants.
internal enum LegacyOrdinaryAdmissionAllocationFailure { None, Rejected, AlreadyCompleted, RecoveryRequired }

internal sealed partial class LegacyOrdinaryAdmissionUidOwner
{
    private bool TryReadStarterHistory(out LegacyItemUidProducerIntentSnapshot[] all,
        out LegacyItemUidProducerIntentSnapshot[] active, out string reason)
    {
        all = []; active = [];
        try
        {
            if (!registry.TryReadProducerIntents(domain, out var observed, out reason) ||
                !registry.TryReadActiveProducerIntents(domain, out var live, out reason)) return false;
            all = observed.Where(row => row.Request.Writer == Writer).ToArray();
            active = live.Where(row => row.Request.Writer == Writer).ToArray();
            var liveIds = active.Select(row => row.Request.OperationId).ToHashSet();
            foreach (var row in all) _ = ReadRecipe(row);
            foreach (var group in all.GroupBy(row => row.Request.CharacterUid))
            {
                var completed = group.Where(row => !liveIds.Contains(row.Request.OperationId)).ToArray();
                var unfinished = group.Where(row => liveIds.Contains(row.Request.OperationId)).ToArray();
                if (completed.Length > 1 || unfinished.Length > 1 || completed.Length != 0 && unfinished.Length != 0 ||
                    completed.Any(row => row.Allocation is null))
                { reason = "ordinaryAdmissionRecoveryRequired:conflictingRetainedHistory"; return false; }
            }
            reason = string.Empty; return true;
        }
        catch (Exception error) when (OwnerFailure(error))
        { reason = "ordinaryAdmissionRecoveryRequired:retainedHistory:" + error.GetType().Name; return false; }
    }
}
