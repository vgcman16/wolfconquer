using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private void Write(Record row)
    {
        var operation = Guid.ParseExact(row.Operation, "N");
        EnsureHistoryLayout();
        SnapshotFileLockCoordinator.ExecuteLocked(directory, () =>
        {
            ValidateRootMembership();
            RequireHistoryBinding(row);
            var index = ReadIndex();
            RequireKnownAnchor(index);
            var pending = index.Pending.SingleOrDefault(entry => entry.Operation == row.Operation);
            var completed = ReadCompleted(operation);
            var prior = pending ?? completed;
            if (prior is not null && !Same(prior, row) || completed is not null && !row.Completed)
                throw new InvalidOperationException("Package work cannot be replaced or reopened.");
            if (row.Completed)
            {
                if (prior is null) throw new InvalidOperationException("Unprepared work cannot be completed.");
                PutCompleted(row);
            }
            else if (pending is null)
            {
                var keys = HistoryLineages(row);
                if (keys.Any(key => completedLineageOperations.ContainsKey(LineageName(row.AccountId, row.CharacterUid, key))) ||
                    index.Pending.Any(other => other.AccountId == row.AccountId && other.CharacterUid == row.CharacterUid &&
                        HistoryLineages(other).Intersect(keys, StringComparer.Ordinal).Any()))
                    throw new InvalidOperationException("Package lineage already has retained work.");
                if (index.Pending.Length >= MaximumPendingRecords)
                    throw new InvalidOperationException("Pending package work capacity is exhausted.");
                WriteIndex(index with { Pending = index.Pending.Append(row).OrderBy(entry => entry.Operation, StringComparer.Ordinal).ToArray() });
            }
            var saved = Read(operation);
            if (saved is null || !Same(saved, row) || saved.Completed != row.Completed)
                throw new IOException("Package work publication is unproven.");
            return true;
        });
    }

    private static void EnsureOrdinary(string path)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        file.Refresh();
        if (Directory.Exists(path)) throw new IOException("Package work target is not a file.");
        if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Package work link is refused.");
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if (File.Exists(parent.FullName)) throw new IOException("Package work ancestor is not a directory.");
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Package work ancestor link is refused.");
        }
    }
}
