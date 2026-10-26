using System.Text.Json;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private void ValidateRootMembership()
    {
        EnsureOrdinary(IndexPath);
        if (!Directory.Exists(directory)) throw new InvalidDataException("History layout is missing.");
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var leaf = Path.GetFileName(path);
            var expectedDirectory = leaf is "completed" or "lineages" or "legacy";
            if (leaf is not (".index.json" or ".layout-migration.json" or "completed" or "lineages" or "legacy") ||
                Directory.Exists(path) != expectedDirectory) throw new InvalidDataException("Unknown history layout entry.");
            EnsureOrdinary(expectedDirectory ? Path.Combine(path, ".ordinary-check") : path);
        }
    }

    private IEnumerable<string> EnumerateShardFiles(string root, int nameLength)
    {
        EnsureOrdinary(Path.Combine(root, ".ordinary-check"));
        if (!Directory.Exists(root)) yield break;
        foreach (var shard in Directory.EnumerateFileSystemEntries(root))
        {
            var prefix = Path.GetFileName(shard);
            if (!Directory.Exists(shard) || prefix.Length != 2 || prefix.Any(value => value is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
                throw new InvalidDataException("Unknown history shard.");
            EnsureOrdinary(Path.Combine(shard, ".ordinary-check"));
            var hasFile = false;
            foreach (var path in Directory.EnumerateFileSystemEntries(shard))
            {
                var leaf = Path.GetFileName(path);
                if (Directory.Exists(path) || leaf.Length != nameLength + 5 || !leaf.EndsWith(".json", StringComparison.Ordinal) ||
                    !leaf.StartsWith(prefix, StringComparison.Ordinal) ||
                    leaf[..nameLength].Any(value => value is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
                    throw new InvalidDataException("Unknown history archive entry.");
                EnsureOrdinary(path);
                hasFile = true;
                yield return path;
            }
            if (!hasFile) throw new InvalidDataException("Unowned empty history shard.");
        }
    }

    private void ValidateCompletedMembership(PendingIndex index)
    {
        ulong files = 0;
        foreach (var path in EnumerateShardFiles(CompletedRoot, 32))
        {
            if (!completedHashes.ContainsKey(Path.GetFileNameWithoutExtension(path)))
                throw new InvalidDataException("Unanchored completed history entry.");
            files++;
        }
        if (files != index.CompletedCount) throw new InvalidDataException("Completed archive membership differs.");
        foreach (var record in EnumerateCompleted())
        {
            var retained = index.Pending.SingleOrDefault(row => row.Operation == record.Operation);
            if (retained is not null) throw new InvalidDataException("Completed work remains pending in its anchored index.");
            foreach (var key in HistoryLineages(record))
            {
                var lineage = ReadLineage(LineagePath(record.AccountId, record.CharacterUid, key));
                if (lineage is null || lineage.Operation != record.Operation ||
                    lineage.RecordSha256 != Digest(JsonSerializer.SerializeToUtf8Bytes(record, HistoryJson)))
                    throw new InvalidDataException("Completed lineage history is missing or changed.");
            }
        }
        foreach (var path in EnumerateShardFiles(LineageRoot, 64))
        {
            var lineage = ReadLineage(path) ?? throw new IOException("Lineage history disappeared.");
            var record = ReadCompleted(Guid.ParseExact(lineage.Operation, "N")) ?? throw new InvalidDataException("Orphan history lineage.");
            RequireHistoryBinding(record);
            if (lineage.AccountId != record.AccountId || lineage.CharacterUid != record.CharacterUid ||
                !HistoryLineages(record).Contains(lineage.Lineage, StringComparer.Ordinal) ||
                lineage.RecordSha256 != Digest(JsonSerializer.SerializeToUtf8Bytes(record, HistoryJson)))
                throw new InvalidDataException("History lineage identity differs.");
        }
    }
}
