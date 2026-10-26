using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    private sealed record PendingPackageWork(uint AccountId, uint CharacterUid, Guid OperationId);
    internal bool TryAdmitRequest(long account, uint uid, LegacyMailboxModePacket request, out string reason)
    {
        reason = "mailboxClaimRecoveryRequired:packagePending";
        try
        {
            if (!HasPendingAccountWork(account, out var pending, out reason)) return false;
            if (!pending) return true;
            if (!TryGetSinglePending(account, out var intent, out var description, out reason)) return false;
            if (intent!.CharacterUid != uid || description!.Request.Type != request.Type ||
                description.Request.Id != request.Id || !description.Request.Items.SequenceEqual(request.Items))
            { reason = "mailboxClaimRecoveryRequired:packageReplayRequestMismatch"; return false; }
            var paths = WorkPaths(intent.AccountId, uid);
            if (!SnapshotFileLockCoordinator.ExecuteLocked(paths, () => HashesMatch(paths, description.Before)))
            { reason = "mailboxClaimRecoveryRequired:packageReplayImagesChanged"; return false; }
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:packageAdmission:" + error.GetType().Name; return false; }
    }

    private bool TryGetSinglePending(long account, out PendingPackageWork? pending,
        out LegacyMailboxPackageRecipe.WorkDescription? description, out string reason)
    {
        pending = null;
        description = null;
        reason = "mailboxClaimRecoveryRequired:packagePending";
        if (!Registry.TryReadActiveProducerIntents(Domain, out var intents, out reason)) return false;
        foreach (var intent in intents.Where(row => row.Request.Writer == Writer && row.Request.AccountId == account))
        {
            if (!TryDescribe(intent, out var parsed)) return false;
            var work = workStore.Read(intent.Request.OperationId);
            if (work is not null && !workStore.MatchesIntent(work, intent)) return false;
            if (work is { Completed: true }) continue;
            if (pending is not null)
            { reason = "mailboxClaimRecoveryRequired:multiplePackageIntents"; return false; }
            pending = new(intent.Request.AccountId, intent.Request.CharacterUid, intent.Request.OperationId);
            description = parsed;
        }
        foreach (var row in workStore.ReadPendingNoAllocationRecords().Where(row => row.AccountId == account))
        {
            if (!TryValidateNoAllocationRecord(row, out var parsed)) return false;
            if (row.Completed) continue;
            if (pending is not null)
            { reason = "mailboxClaimRecoveryRequired:multiplePackageIntents"; return false; }
            pending = new(row.AccountId, row.CharacterUid, Guid.ParseExact(row.Operation, "N"));
            description = parsed;
        }
        reason = "mailboxClaimRecoveryRequired:packagePending";
        return pending is not null;
    }
}
