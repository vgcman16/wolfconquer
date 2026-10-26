using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    internal Record PrepareWork(LegacyItemUidProducerIntentSnapshot intent, IReadOnlyList<LegacyTradeJournalChange> changes)
    {
        var row = CreateRecord(intent, changes);
        RequireCurrentIntent(row);
        if (!ImagesMatch(row, after: false)) throw new InvalidOperationException("Package work preimages changed.");
        var prior = Read(intent.Request.OperationId);
        if (prior is { Completed: true }) throw new InvalidOperationException("Completed package work cannot reopen.");
        Write(row);
        return row;
    }

    internal void CompleteWork(Record row, LegacyMailboxPackageCommitProof proof)
    {
        RequireCurrentIntent(row);
        var prior = Read(Guid.ParseExact(row.Operation, "N"));
        if (prior is null || !Same(prior, row) || !proof.MatchesPackageWork(row) || !ImagesMatch(row, after: true))
            throw new InvalidOperationException("Package completion lacks its actual joint outcome.");
        Write(row with { Completed = true });
    }

    internal bool Reconcile(LegacyItemUidProducerIntentSnapshot intent, Record row, out bool pending)
    {
        pending = true;
        if (!MatchesIntent(row, intent)) return false;
        if (row.Completed) { pending = false; return true; }
        var remainsPending = true;
        var valid = SnapshotFileLockCoordinator.ExecuteLocked(TargetPaths(row), () =>
        {
            RequireCurrentIntent(row);
            var current = Read(intent.Request.OperationId);
            if (current is null || !Same(current, row)) return false;
            if (current.Completed) { remainsPending = false; return true; }
            if (ImagesMatch(current, after: true))
            {
                // This is actual retained-work recovery: the immutable prepared
                // range/image record plus four exact postimages, after startup's
                // real v5 prefix recovery. No existence-only inference or callback.
                Write(current with { Completed = true });
                remainsPending = false;
                return true;
            }
            return ImagesMatch(current, after: false);
        });
        pending = remainsPending;
        return valid;
    }

    private void RequireCurrentIntent(Record row)
    {
        if (!owner.IsCurrent(owner.UsersRoot, owner.ItemsRoot, owner.MailboxRoot, owner.Recovery) ||
            !owner.Registry.TryReadProducerIntent(domain, Guid.ParseExact(row.Operation, "N"), out var actual, out _) ||
            actual is null || !MatchesIntent(row, actual))
            throw new InvalidOperationException("Package work has no current real producer intent.");
    }
}
