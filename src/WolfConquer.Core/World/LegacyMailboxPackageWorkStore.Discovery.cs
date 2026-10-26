namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    internal Record[] ReadPendingNoAllocationRecords()
    {
        EnsureHistoryLayout();
        return WolfConquer.Core.Data.SnapshotFileLockCoordinator.ExecuteLocked(directory, () =>
        {
            ValidateRootMembership();
            var index = ReadIndex();
            RequireKnownAnchor(index);
            var pending = index.Pending;
            foreach (var row in pending) RequireHistoryBinding(row);
            return pending.Where(row => row.Version == 2).ToArray();
        });
    }

    // Explicit cold inventory. Packet admission uses the bounded pending index instead.
    internal Record[] ReadNoAllocationRecords()
    {
        EnsureHistoryLayout();
        return WolfConquer.Core.Data.SnapshotFileLockCoordinator.ExecuteLocked(directory, () =>
        {
            ValidateRootMembership();
            var index = ReadIndex();
            RequireKnownAnchor(index);
            var pending = index.Pending;
            foreach (var row in pending) RequireHistoryBinding(row);
            return pending.Concat(EnumerateCompleted()).Where(row => row.Version == 2)
                .DistinctBy(row => row.Operation).OrderBy(row => row.Operation, StringComparer.Ordinal).ToArray();
        });
    }
}
