using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private void LoadCompletedChain(PendingIndex index)
    {
        completedHashes.Clear();
        completedLineageOperations.Clear();
        var operation = index.CompletedHead;
        var hash = index.CompletedHeadSha256;
        for (ulong count = 0; count < index.CompletedCount; count++)
        {
            if (operation == Guid.Empty.ToString("N") || !completedHashes.TryAdd(operation, hash))
                throw new InvalidDataException("Completed history chain is short or cyclic.");
            var entry = ReadCompletedEntry(Guid.ParseExact(operation, "N"));
            if (entry is null || entry.Value.Hash != hash) throw new InvalidDataException("Completed history chain is missing or changed.");
            RequireHistoryBinding(entry.Value.Entry.Work);
            var work = entry.Value.Entry.Work;
            foreach (var lineage in HistoryLineages(work))
                if (!completedLineageOperations.TryAdd(LineageName(work.AccountId, work.CharacterUid, lineage), work.Operation))
                    throw new InvalidDataException("Completed history has a duplicate reward lineage.");
            operation = entry.Value.Entry.PreviousOperation;
            hash = entry.Value.Entry.PreviousSha256;
        }
        if (operation != Guid.Empty.ToString("N") || hash != EmptyArchiveHash)
            throw new InvalidDataException("Completed history chain exceeds its anchor count.");
        RememberAnchor(index);
    }

    private void RecoverKnownCompletionTail()
    {
        var extras = EnumerateShardFiles(CompletedRoot, 32)
            .Where(path => !completedHashes.ContainsKey(Path.GetFileNameWithoutExtension(path))).Take(2).ToArray();
        if (extras.Length == 0) return;
        if (extras.Length != 1) throw new InvalidDataException("Unbound completed history entries exist.");
        var operation = Guid.ParseExact(Path.GetFileNameWithoutExtension(extras[0]), "N");
        var entry = ReadCompletedEntry(operation) ?? throw new IOException("Completion recovery entry disappeared.");
        var index = ReadIndex();
        var work = entry.Entry.Work;
        if (entry.Entry.PreviousOperation != index.CompletedHead || entry.Entry.PreviousSha256 != index.CompletedHeadSha256)
            throw new InvalidDataException("Unbound completion predecessor differs.");
        var pending = index.Pending.SingleOrDefault(row => row.Operation == work.Operation);
        if (pending is not null && Same(pending, work))
        {
            if (!SnapshotFileLockCoordinator.ExecuteLocked(TargetPaths(work), () => ImagesMatch(work, after: true)))
                throw new InvalidDataException("Interrupted completion lacks exact committed images.");
        }
        else if (!IsRetainedMigrationCompletion(work)) throw new InvalidDataException("Unowned completed history tail.");
        PutCompleted(work);
    }

    private bool IsRetainedMigrationCompletion(Record work)
    {
        if (!File.Exists(MigrationPath)) return false;
        var migration = ReadMigration();
        if (migration.Completed) return false;
        var entry = migration.Entries.SingleOrDefault(row => row.Operation == work.Operation);
        if (entry is null) return false;
        var original = WorkPath(Guid.ParseExact(work.Operation, "N"));
        var moved = Path.Combine(LegacyRoot, work.Operation + ".json");
        if (File.Exists(original) == File.Exists(moved)) return false;
        var bytes = ReadHistoryBytes(File.Exists(original) ? original : moved, MaximumBytes);
        if (Digest(bytes) != entry.Sha256) return false;
        var prior = ParseRecord(bytes, Guid.ParseExact(work.Operation, "N"));
        return prior.Completed && Same(prior, work);
    }
}
