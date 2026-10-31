using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private const int MaximumArchiveBytes = 32 * 1024;
    private sealed record ProducerArchive(int Version, string Domain, string OperationId,
        string? PreviousOperation, string? PreviousSha256, ulong BeforeGeneration, ulong AfterGeneration,
        string BeforeStateSha256, ProducerIntent Intent, OperationReservation Allocation);
    private sealed record ArchiveEntry(ProducerArchive Record, byte[] Bytes, string Sha256);
    private sealed record ArchiveCatalog(Dictionary<string, ArchiveEntry> Entries, ArchiveEntry? Next, bool Staged);
    private sealed record ArchiveIdentity(string Sha256, uint AccountId, uint CharacterUid, ulong PreparedGeneration);
    private sealed record ArchiveIdentityIndex(string? Head, string? HeadSha256, ulong? Count,
        Dictionary<string, ArchiveIdentity> Entries);
    private ArchiveIdentityIndex? archiveIndex;
    private static readonly string[] ArchiveKeys = ["Version", "Domain", "OperationId", "PreviousOperation",
        "PreviousSha256", "BeforeGeneration", "AfterGeneration", "BeforeStateSha256", "Intent", "Allocation"];

    private static bool IsCanonicalOperation(string? text) => Guid.TryParseExact(text, "N", out var id) &&
        id != Guid.Empty && id.ToString("N") == text;

    private static string ArchiveDigest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string ArchivePath(RegistryPaths paths, string operation) => Path.Combine(paths.History, operation + ".json");
    private static byte[] ArchiveBytes(ProducerArchive row) => JsonSerializer.SerializeToUtf8Bytes(row);

    private static ArchiveEntry ReadArchive(string path, RegistryState current)
    {
        EnsureOrdinaryPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumArchiveBytes) throw new JsonException("Invalid UID archive size.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || stream.Length != bytes.Length) throw new IOException("UID archive changed length.");
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
        RequireArchiveKeys(json.RootElement, ArchiveKeys);
        RequireArchiveKeys(json.RootElement.GetProperty("Intent"), ProducerIntentKeys);
        RequireArchiveKeys(json.RootElement.GetProperty("Allocation"), OperationKeys);
        var row = JsonSerializer.Deserialize<ProducerArchive>(bytes) ?? throw new JsonException("Missing UID archive.");
        if (row.Version != 1 || row.Domain != current.Domain || !IsCanonicalOperation(row.OperationId) ||
            row.Intent is null || row.Allocation is null || row.Intent.OperationId != row.OperationId ||
            row.Allocation.OperationId != row.OperationId ||
            (row.Intent.Writer != LegacyMailboxPackageProducer.Writer && row.Intent.Writer != LegacyOrdinaryAdmissionUidOwner.Writer) ||
            row.BeforeGeneration == ulong.MaxValue || row.AfterGeneration != row.BeforeGeneration + 1 ||
            !IsDigest(row.BeforeStateSha256) ||
            (row.PreviousOperation is null ? row.PreviousSha256 is not null :
                !IsCanonicalOperation(row.PreviousOperation) || !IsDigest(row.PreviousSha256)) ||
            !ArchiveBytes(row).AsSpan().SequenceEqual(bytes))
            throw new JsonException("Invalid or noncanonical UID archive binding.");
        var synthetic = current with { Version = 3, Operations = [row.Allocation], ProducerIntents = [row.Intent],
            ArchiveHead = null, ArchiveHeadSha256 = null, ArchiveCount = null,
            Generation = Math.Max(current.Generation, row.AfterGeneration) };
        ValidateOperationState(synthetic);
        ValidateProducerIntentState(synthetic);
        if (row.Allocation.Generation >= row.AfterGeneration)
            throw new JsonException("UID archive retirement precedes its allocation.");
        return new(row, bytes, ArchiveDigest(bytes));
    }

    private static void RequireArchiveKeys(JsonElement value, string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException("UID archive member is not an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!expected.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                throw new JsonException("Unknown or duplicate UID archive property.");
        if (names.Count != expected.Length) throw new JsonException("Missing UID archive property.");
    }

    private static RegistryState RetiredState(RegistryState before, ArchiveEntry entry) => before with
    {
        Version = Math.Max(4, before.Version), Generation = entry.Record.AfterGeneration,
        Operations = (before.Operations ?? []).Where(row => row.OperationId != entry.Record.OperationId).ToArray(),
        ProducerIntents = (before.ProducerIntents ?? []).Where(row => row.OperationId != entry.Record.OperationId).ToArray(),
        ArchiveHead = entry.Record.OperationId, ArchiveHeadSha256 = entry.Sha256,
        ArchiveCount = checked((before.ArchiveCount ?? 0) + 1),
    };

    private static bool IsExactRetirement(RegistryState before, byte[] beforeBytes, RegistryState after, ArchiveEntry entry)
    {
        var row = entry.Record;
        return before.Generation != ulong.MaxValue && row.BeforeGeneration == before.Generation &&
            row.BeforeStateSha256 == ArchiveDigest(beforeBytes) && row.PreviousOperation == before.ArchiveHead &&
            row.PreviousSha256 == before.ArchiveHeadSha256 &&
            (before.ProducerIntents ?? []).Count(item => item == row.Intent) == 1 &&
            (before.Operations ?? []).Count(item => item == row.Allocation) == 1 &&
            SameState(RetiredState(before, entry), after);
    }
}
