using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryGetCompletedWorkProof(Guid operation,
        out LegacyMailboxPackageCompletedWorkProof? proof, out string reason) =>
        LegacyMailboxPackageCompletedWorkProof.TryCapture(this, operation, out proof, out reason);

    internal bool TryReadCompletedIntent(Guid operation,
        out LegacyItemUidProducerIntentSnapshot? intent, out string reason)
    {
        intent = null;
        reason = "mailboxPackageCompletionUnproven";
        try
        {
            if (operation == Guid.Empty || !IsCurrent(UsersRoot, ItemsRoot, MailboxRoot, Recovery) ||
                !Registry.TryReadProducerIntent(Domain, operation, out var actual, out reason) || actual is null) return false;
            var work = workStore.Read(operation);
            if (work is not { Completed: true } || !workStore.MatchesIntent(work, actual) ||
                awaitingSessionPublication.TryGetValue(work.AccountId, out var waiting) && waiting == operation)
            { reason = "mailboxPackageCompletionUnproven"; return false; }
            intent = actual;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:completedHistory:" + error.GetType().Name; return false; }
    }

    private bool TryRelieveCompletedIntentPressure(int incomingBytes, out string reason, bool force = false)
    {
        try { return TryRetireCompletedIntents(incomingBytes, force, out reason); }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:historyRetirement:" + error.GetType().Name; return false; }
    }

    private bool TryRetireCompletedIntents(int incomingBytes, bool force, out string reason)
    {
        if (!Registry.TryReadActiveProducerIntents(Domain, out var active, out reason)) return false;
        // Leave ample bounded active capacity without adding a state generation
        // to every successful claim. No-allocation work never calls this path.
        var bytes = active.Sum(row => row.CopyPayload().Length);
        if (!force && active.Length < LegacyItemUidRegistry.MaximumProducerIntents / 2 &&
            bytes + incomingBytes < LegacyItemUidRegistry.MaximumTotalProducerPayloadBytes * 3 / 4) return true;
        foreach (var intent in active.Where(row => row.Request.Writer == Writer && row.Allocation is not null))
        {
            var row = workStore.Read(intent.Request.OperationId);
            if (row is not { Completed: true }) continue;
            if (awaitingSessionPublication.TryGetValue(row.AccountId, out var waiting) && waiting == intent.Request.OperationId) continue;
            if (!TryGetCompletedWorkProof(intent.Request.OperationId, out var proof, out reason) ||
                !Registry.TryArchiveCompletedProducerIntent(Domain, intent.Request.OperationId, proof!, out reason)) return false;
        }
        reason = string.Empty;
        return true;
    }
}
