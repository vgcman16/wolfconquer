namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private void ResumeMigration(LayoutMigration migration, LegacyMailboxHistoryInterruption interruption)
    {
        if (migration.Completed)
        {
            var index = ReadIndex();
            if (index.OriginMigration != migration.Id) throw new InvalidDataException("History index has different migration provenance.");
            // Current pending/anchored-completed authority must be admitted before
            // validating historical custody copies. No recursive layout admission.
            LoadCompletedChain(index);
            ValidateLegacy(migration);
            return;
        }
        var pending = new List<Record>();
        var completed = new List<Record>();
        foreach (var entry in migration.Entries)
        {
            var source = WorkPath(Guid.ParseExact(entry.Operation, "N"));
            var destination = Path.Combine(LegacyRoot, entry.Operation + ".json");
            if (File.Exists(source) == File.Exists(destination)) throw new InvalidDataException("Migration source custody is ambiguous.");
            var path = File.Exists(source) ? source : destination;
            var bytes = ReadHistoryBytes(path, MaximumBytes);
            if (Digest(bytes) != entry.Sha256) throw new InvalidDataException("Retained legacy work changed.");
            var row = ParseRecord(bytes, Guid.ParseExact(entry.Operation, "N"));
            RequireHistoryBinding(row);
            if (row.Completed) completed.Add(row);
            else pending.Add(row);
        }
        var expected = EmptyIndex(domain, migration.Id, pending.OrderBy(row => row.Operation, StringComparer.Ordinal).ToArray());
        if (File.Exists(IndexPath))
        {
            var current = ReadIndex();
            if (current.OriginMigration != expected.OriginMigration || current.Pending.Length != expected.Pending.Length ||
                !current.Pending.Zip(expected.Pending).All(pair => Same(pair.First, pair.Second)))
                throw new InvalidDataException("Migration index differs from its retained sources.");
        }
        else WriteIndex(expected);
        StopHistory(interruption, LegacyMailboxHistoryInterruption.AfterIndex);
        LoadCompletedChain(ReadIndex());
        RecoverKnownCompletionTail();
        foreach (var row in completed) PutCompleted(row);
        StopHistory(interruption, LegacyMailboxHistoryInterruption.AfterArchives);
        Directory.CreateDirectory(LegacyRoot);
        EnsureOrdinary(Path.Combine(LegacyRoot, ".ordinary-check"));
        var moved = 0;
        foreach (var entry in migration.Entries)
        {
            var source = WorkPath(Guid.ParseExact(entry.Operation, "N"));
            var destination = Path.Combine(LegacyRoot, entry.Operation + ".json");
            if (File.Exists(source))
            {
                EnsureOrdinary(source); EnsureOrdinary(destination);
                File.Move(source, destination, overwrite: false); // exact bytes retained, never deleted.
                if (++moved == 1) StopHistory(interruption, LegacyMailboxHistoryInterruption.AfterFirstMove);
            }
        }
        StopHistory(interruption, LegacyMailboxHistoryInterruption.AfterAllMoves);
        ValidateLegacy(migration);
        WriteMigration(migration with { Completed = true });
    }

    private void ValidateLegacy(LayoutMigration migration)
    {
        if (!Directory.Exists(LegacyRoot)) throw new InvalidDataException("Retained legacy history is missing.");
        var index = ReadIndex();
        RequireKnownAnchor(index);
        var expected = migration.Entries.ToDictionary(entry => entry.Operation + ".json", StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(LegacyRoot))
        {
            var leaf = Path.GetFileName(path);
            if (Directory.Exists(path) || !expected.TryGetValue(leaf, out var entry) || !seen.Add(leaf))
                throw new InvalidDataException("Unknown retained legacy history entry.");
            var bytes = ReadHistoryBytes(path, MaximumBytes);
            if (Digest(bytes) != entry.Sha256) throw new InvalidDataException("Retained legacy history differs.");
            RequireLegacyCounterpart(ParseRecord(bytes, Guid.ParseExact(entry.Operation, "N")), index);
        }
        if (seen.Count != expected.Count) throw new InvalidDataException("Retained legacy history entry is missing.");
    }
}
