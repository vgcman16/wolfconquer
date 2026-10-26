using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyMailboxHistoryInterruption { None, AfterMigrationRecord, AfterArchives, AfterIndex, AfterFirstMove, AfterAllMoves }

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private sealed record MigrationEntry(string Operation, string Sha256);
    private sealed record LayoutMigration(int Version, string Domain, string Id, bool Completed, MigrationEntry[] Entries);
    private string MigrationPath => Path.Combine(directory, ".layout-migration.json");
    private string LegacyRoot => Path.Combine(directory, "legacy");

    internal void EnsureHistoryLayout() => EnsureHistoryLayout(LegacyMailboxHistoryInterruption.None);

    internal void EnsureHistoryLayout(LegacyMailboxHistoryInterruption interruption)
    {
        if (!Enum.IsDefined(interruption)) throw new ArgumentException("Invalid history interruption.");
        SnapshotFileLockCoordinator.ExecuteLocked(directory, () =>
        {
            if (historyReady) { ValidateRootMembership(); return true; }
            EnsureOrdinary(IndexPath);
            var existed = Directory.Exists(directory);
            Directory.CreateDirectory(directory);
            if (File.Exists(MigrationPath)) ResumeMigration(ReadMigration(), interruption);
            else if (!File.Exists(IndexPath))
            {
                var entries = CaptureLegacyEntries();
                if (entries.Length == 0)
                {
                    if (existed) throw new InvalidDataException("Existing work namespace lost its authoritative index.");
                    WriteIndex(EmptyIndex(domain, Guid.Empty.ToString("N"), []));
                }
                else
                {
                    var migration = new LayoutMigration(1, domain.ToString("N"), Guid.NewGuid().ToString("N"), false, entries);
                    WriteMigration(migration);
                    StopHistory(interruption, LegacyMailboxHistoryInterruption.AfterMigrationRecord);
                    ResumeMigration(migration, interruption);
                }
            }
            ValidateRootMembership();
            var index = ReadIndex();
            if (index.OriginMigration != Guid.Empty.ToString("N") && !File.Exists(MigrationPath))
                throw new InvalidDataException("History migration provenance is missing.");
            LoadCompletedChain(index);
            RecoverKnownCompletionTail();
            index = ReadIndex();
            foreach (var row in index.Pending) RequireHistoryBinding(row);
            ValidateCompletedMembership(index);
            if (File.Exists(MigrationPath)) ValidateLegacy(ReadMigration());
            else if (Directory.Exists(LegacyRoot)) throw new InvalidDataException("Unowned legacy history exists.");
            historyReady = true;
            return true;
        });
    }

    private MigrationEntry[] CaptureLegacyEntries()
    {
        var rows = new List<MigrationEntry>();
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            if (rows.Count >= 256) throw new InvalidDataException("Legacy work layout exceeds its original bound.");
            var leaf = Path.GetFileName(path);
            if (Directory.Exists(path) || leaf.Length != 37 || !leaf.EndsWith(".json", StringComparison.Ordinal) ||
                !Guid.TryParseExact(leaf[..32], "N", out var operation) || operation == Guid.Empty || operation.ToString("N") != leaf[..32])
                throw new InvalidDataException("Unknown legacy work entry.");
            var bytes = ReadHistoryBytes(path, MaximumBytes);
            var row = ParseRecord(bytes, operation);
            RequireHistoryBinding(row);
            rows.Add(new(row.Operation, Digest(bytes)));
        }
        return rows.OrderBy(row => row.Operation, StringComparer.Ordinal).ToArray();
    }

    private LayoutMigration ReadMigration()
    {
        var bytes = ReadHistoryBytes(MigrationPath, 128 * 1024);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 3 });
        RequireProperties(document.RootElement, ["Version", "Domain", "Id", "Completed", "Entries"]);
        var row = JsonSerializer.Deserialize<LayoutMigration>(bytes) ?? throw new InvalidDataException("Migration is missing.");
        if (row.Version != 1 || row.Domain != domain.ToString("N") || !Guid.TryParseExact(row.Id, "N", out var id) || id == Guid.Empty ||
            id.ToString("N") != row.Id || row.Entries is not { Length: >= 1 and <= 256 } ||
            row.Entries.Any(entry => entry is null || !Guid.TryParseExact(entry.Operation, "N", out var operation) || operation == Guid.Empty ||
                operation.ToString("N") != entry.Operation || !IsDigest(entry.Sha256)) ||
            row.Entries.Select(entry => entry.Operation).Distinct(StringComparer.Ordinal).Count() != row.Entries.Length)
            throw new InvalidDataException("Migration bindings are invalid.");
        foreach (var entry in document.RootElement.GetProperty("Entries").EnumerateArray())
            RequireProperties(entry, ["Operation", "Sha256"]);
        return row;
    }

    private void WriteMigration(LayoutMigration row)
    {
        WriteHistoryAtomically(MigrationPath, JsonSerializer.SerializeToUtf8Bytes(row));
        if (ReadMigration() is not { } saved || saved.Id != row.Id || saved.Completed != row.Completed ||
            !saved.Entries.SequenceEqual(row.Entries)) throw new IOException("Migration publication is unproven.");
    }

    private static void StopHistory(LegacyMailboxHistoryInterruption actual, LegacyMailboxHistoryInterruption boundary)
    { if (actual == boundary) throw new IOException("mailboxClaimRecoveryRequired:historyMigrationInterrupted"); }
}
