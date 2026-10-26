using System.Text.Json;
using System.Text.Json.Serialization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private const int MaximumPendingRecords = 256;
    private const int MaximumIndexBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions HistoryJson = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private const string EmptyArchiveHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private sealed record PendingIndex(int LayoutVersion, string Domain, string OriginMigration, Record[] Pending,
        string CompletedHead, string CompletedHeadSha256, ulong CompletedCount);
    private readonly Dictionary<string, string> completedHashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> completedLineageOperations = new(StringComparer.Ordinal);
    private string knownHead = Guid.Empty.ToString("N"), knownHeadHash = EmptyArchiveHash;
    private ulong knownCompletedCount;
    private bool historyReady;
    private string IndexPath => Path.Combine(directory, ".index.json");

    internal Record? Read(Guid operation)
    {
        EnsureHistoryLayout();
        return SnapshotFileLockCoordinator.ExecuteLocked(directory, () =>
        {
            ValidateRootMembership();
            var index = ReadIndex();
            RequireKnownAnchor(index);
            var active = index.Pending.SingleOrDefault(row => row.Operation == operation.ToString("N"));
            return active ?? ReadCompleted(operation);
        });
    }

    private PendingIndex ReadIndex()
    {
        var bytes = ReadHistoryBytes(IndexPath, MaximumIndexBytes);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 5 });
        RequireProperties(document.RootElement, ["LayoutVersion", "Domain", "OriginMigration", "Pending", "CompletedHead", "CompletedHeadSha256", "CompletedCount"]);
        var root = document.RootElement;
        if (root.GetProperty("LayoutVersion").GetInt32() != 1 || root.GetProperty("Domain").GetString() != domain.ToString("N") ||
            !Guid.TryParseExact(root.GetProperty("OriginMigration").GetString(), "N", out var origin) ||
            origin.ToString("N") != root.GetProperty("OriginMigration").GetString()) throw new InvalidDataException("Work index binding is invalid.");
        var array = root.GetProperty("Pending");
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > MaximumPendingRecords)
            throw new InvalidDataException("Pending work index exceeds its bound.");
        var records = new List<Record>();
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("Operation", out var identity) ||
                identity.ValueKind != JsonValueKind.String) throw new InvalidDataException("Pending work identity shape is invalid.");
            var operation = identity.GetString();
            if (!Guid.TryParseExact(operation, "N", out var id) || id == Guid.Empty || id.ToString("N") != operation ||
                !operations.Add(operation)) throw new InvalidDataException("Pending work identity is invalid.");
            var row = ParseRecord(JsonSerializer.SerializeToUtf8Bytes(element), id);
            if (row.Completed) throw new InvalidDataException("Pending index cannot contain a completed record.");
            records.Add(row);
        }
        var head = root.GetProperty("CompletedHead").GetString();
        var hash = root.GetProperty("CompletedHeadSha256").GetString();
        var count = root.GetProperty("CompletedCount").GetUInt64();
        if (!Guid.TryParseExact(head, "N", out var headId) || headId.ToString("N") != head || !IsDigest(hash) ||
            (count == 0 ? headId != Guid.Empty || hash != EmptyArchiveHash : headId == Guid.Empty || hash == EmptyArchiveHash))
            throw new InvalidDataException("Completed history anchor is invalid.");
        return new(1, domain.ToString("N"), origin.ToString("N"), records.ToArray(), head!, hash!, count);
    }

    private void WriteIndex(PendingIndex index)
    {
        if (index.Pending.Length > MaximumPendingRecords) throw new InvalidOperationException("Pending work capacity is exhausted.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(index, HistoryJson);
        if (bytes.Length > MaximumIndexBytes) throw new InvalidDataException("Pending work bytes exceed their bound.");
        WriteHistoryAtomically(IndexPath, bytes);
        var saved = ReadIndex();
        if (saved.OriginMigration != index.OriginMigration || saved.CompletedHead != index.CompletedHead ||
            saved.CompletedHeadSha256 != index.CompletedHeadSha256 || saved.CompletedCount != index.CompletedCount ||
            saved.Pending.Length != index.Pending.Length ||
            !saved.Pending.Zip(index.Pending).All(pair => Same(pair.First, pair.Second)))
            throw new IOException("Pending work publication is unproven.");
    }

    private static PendingIndex EmptyIndex(Guid domain, string origin, Record[] pending) =>
        new(1, domain.ToString("N"), origin, pending, Guid.Empty.ToString("N"), EmptyArchiveHash, 0);

    private void RememberAnchor(PendingIndex index)
    { knownHead = index.CompletedHead; knownHeadHash = index.CompletedHeadSha256; knownCompletedCount = index.CompletedCount; }

    private void RequireKnownAnchor(PendingIndex index)
    {
        if (index.CompletedHead != knownHead || index.CompletedHeadSha256 != knownHeadHash || index.CompletedCount != knownCompletedCount)
            throw new InvalidDataException("Completed history anchor changed outside its owner.");
    }

    private PendingIndex ReadAdmittedIndex() => SnapshotFileLockCoordinator.ExecuteLocked(directory, () =>
    {
        ValidateRootMembership();
        var index = ReadIndex();
        RequireKnownAnchor(index);
        return index;
    });

    private static void RequireProperties(JsonElement element, string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("History object is invalid.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!expected.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                throw new InvalidDataException("History properties are invalid.");
        if (names.Count != expected.Length) throw new InvalidDataException("History properties are missing.");
    }
}
