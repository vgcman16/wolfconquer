using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private sealed record LineageRecord(int Version, string Domain, uint AccountId, uint CharacterUid,
        string Lineage, string Operation, string RecordSha256);
    private string LineageRoot => Path.Combine(directory, "lineages");

    private string LineageName(uint account, uint uid, string lineage)
    {
        Span<byte> bytes = stackalloc byte[56];
        domain.TryWriteBytes(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[16..], account);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[20..], uid);
        Convert.FromHexString(lineage).CopyTo(bytes[24..]);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private string LineagePath(uint account, uint uid, string lineage)
    {
        var name = LineageName(account, uid, lineage);
        return Path.Combine(LineageRoot, name[..2], name + ".json");
    }

    private LineageRecord? ReadLineage(string path)
    {
        EnsureOrdinary(path);
        if (!File.Exists(path)) return null;
        var bytes = ReadHistoryBytes(path, 4096);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 2 });
        RequireProperties(document.RootElement, ["Version", "Domain", "AccountId", "CharacterUid", "Lineage", "Operation", "RecordSha256"]);
        var row = JsonSerializer.Deserialize<LineageRecord>(bytes) ?? throw new InvalidDataException("History lineage is missing.");
        if (row.Version != 1 || row.Domain != domain.ToString("N") || row.AccountId == 0 || row.CharacterUid == 0 ||
            row.Lineage is not { Length: 64 } || row.Lineage.Any(value => value is not (>= '0' and <= '9' or >= 'A' and <= 'F')) ||
            !Guid.TryParseExact(row.Operation, "N", out var operation) || operation == Guid.Empty || operation.ToString("N") != row.Operation ||
            !IsDigest(row.RecordSha256) || !string.Equals(path, LineagePath(row.AccountId, row.CharacterUid, row.Lineage), StringComparison.Ordinal))
            throw new InvalidDataException("History lineage bindings are invalid.");
        return row;
    }

    private void PutLineage(Record work, string lineage)
    {
        var row = new LineageRecord(1, domain.ToString("N"), work.AccountId, work.CharacterUid, lineage,
            work.Operation, Digest(JsonSerializer.SerializeToUtf8Bytes(work, HistoryJson)));
        var path = LineagePath(work.AccountId, work.CharacterUid, lineage);
        var prior = ReadLineage(path);
        if (prior is not null && prior != row) throw new InvalidOperationException("A completed package lineage is already owned.");
        if (prior is null) WriteHistoryAtomically(path, JsonSerializer.SerializeToUtf8Bytes(row), createOnly: true);
        if (ReadLineage(path) != row) throw new IOException("History lineage publication is unproven.");
    }

    internal bool HasCompletedLineage(uint account, uint uid, string[] keys)
    {
        EnsureHistoryLayout();
        var copy = keys.ToArray();
        return WolfConquer.Core.Data.SnapshotFileLockCoordinator.ExecuteLocked(directory,
            () => HasCompletedLineageCore(account, uid, copy));
    }

    private bool HasCompletedLineageCore(uint account, uint uid, string[] keys)
    {
        _ = ReadAdmittedIndex();
        foreach (var key in keys)
        {
            var expected = completedLineageOperations.TryGetValue(LineageName(account, uid, key), out var expectedOperation);
            var lineage = ReadLineage(LineagePath(account, uid, key));
            if (!expected)
            {
                if (lineage is not null) throw new InvalidDataException("Unanchored lineage history exists.");
                continue;
            }
            if (lineage is null || lineage.Operation != expectedOperation)
                throw new InvalidDataException("Anchored lineage history is missing or changed.");
            var record = ReadCompleted(Guid.ParseExact(lineage.Operation, "N")) ?? throw new InvalidDataException("Lineage history target is missing.");
            RequireHistoryBinding(record);
            if (record.AccountId != account || record.CharacterUid != uid ||
                Digest(JsonSerializer.SerializeToUtf8Bytes(record, HistoryJson)) != lineage.RecordSha256 ||
                !HistoryLineages(record).Contains(key, StringComparer.Ordinal)) throw new InvalidDataException("Lineage history target differs.");
            return true;
        }
        return false;
    }
}
