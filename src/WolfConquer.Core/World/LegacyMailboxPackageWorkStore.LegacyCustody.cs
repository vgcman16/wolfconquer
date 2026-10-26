namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private void RequireLegacyCounterpart(Record original, PendingIndex index)
    {
        var pending = index.Pending.SingleOrDefault(row => row.Operation == original.Operation);
        if (pending is not null && completedHashes.ContainsKey(original.Operation))
            throw new InvalidDataException("Legacy work has ambiguous current ownership.");
        var current = pending ?? ReadCompleted(Guid.ParseExact(original.Operation, "N"));
        if (current is null || !Same(original, current) || original.Completed && !current.Completed)
            throw new InvalidDataException("Legacy custody lacks its exact current work counterpart.");
        // The untouched original may say pending forever. Its authoritative
        // counterpart alone determines active pending versus completed history.
        // Both still bind the actual retained UID request/range (or real no-UID
        // payload); a pending v1 counterpart continues to require active intent.
        RequireHistoryBinding(current);
    }
}
