using System.Text.Json;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private static ArchiveCatalog ReadArchiveCatalog(RegistryPaths paths, RegistryState current,
        byte[]? beforeBytes, bool allowNext)
    {
        var all = new Dictionary<string, ArchiveEntry>(StringComparer.Ordinal);
        EnsureOrdinaryPath(paths.History);
        if (File.Exists(paths.History)) throw new IOException("UID archive directory is not a directory.");
        if (Directory.Exists(paths.History))
            foreach (var path in Directory.EnumerateFileSystemEntries(paths.History))
            {
                EnsureOrdinaryPath(path);
                var leaf = Path.GetFileName(path);
                if (Directory.Exists(path) || leaf.Length != 37 || !leaf.EndsWith(".json", StringComparison.Ordinal) ||
                    !IsCanonicalOperation(leaf[..32])) throw new JsonException("Unknown UID archive entry.");
                var entry = ReadArchive(path, current);
                if (entry.Record.OperationId != leaf[..32] || !all.TryAdd(entry.Record.OperationId, entry))
                    throw new JsonException("UID archive filename or identity differs.");
            }
        var anchored = new Dictionary<string, ArchiveEntry>(StringComparer.Ordinal);
        var operation = current.ArchiveHead;
        var digest = current.ArchiveHeadSha256;
        ulong count = 0, youngerGeneration = ulong.MaxValue;
        while (operation is not null)
        {
            if (!all.TryGetValue(operation, out var entry) || entry.Sha256 != digest ||
                !anchored.TryAdd(operation, entry) || entry.Record.AfterGeneration > current.Generation ||
                entry.Record.AfterGeneration >= youngerGeneration)
                throw new JsonException("UID archive chain is missing, changed or cyclic.");
            count = checked(count + 1);
            if (count > current.ArchiveCount.GetValueOrDefault()) throw new JsonException("UID archive count differs.");
            youngerGeneration = entry.Record.AfterGeneration;
            operation = entry.Record.PreviousOperation; digest = entry.Record.PreviousSha256;
        }
        if (count != current.ArchiveCount.GetValueOrDefault() || digest is not null)
            throw new JsonException("UID archive anchor is incomplete.");
        var extras = all.Where(pair => !anchored.ContainsKey(pair.Key)).Select(pair => pair.Value).Take(2).ToArray();
        ArchiveEntry? next = null;
        var staged = File.Exists(paths.ArchivePending);
        if (staged)
        {
            if (!allowNext || extras.Length != 0) throw new JsonException("Unexpected UID archive stage.");
            next = ReadArchive(paths.ArchivePending, current);
        }
        else if (extras.Length != 0)
        {
            if (!allowNext || extras.Length != 1) throw new JsonException("Unanchored UID archive membership.");
            next = extras[0];
        }
        if (next is not null && (beforeBytes is null || anchored.ContainsKey(next.Record.OperationId) ||
            !IsExactRetirement(current, beforeBytes, RetiredState(current, next), next)))
            throw new JsonException("UID archive stage is not the exact next retirement.");
        ValidateArchiveUnion(current, anchored.Values.Select(entry => entry.Record));
        return new(anchored, next, staged);
    }

    private static void ValidateArchiveUnion(RegistryState current, IEnumerable<ProducerArchive> history)
    {
        var rows = history.ToArray();
        var intents = (current.ProducerIntents ?? []).Concat(rows.Select(row => row.Intent)).ToArray();
        var operations = (current.Operations ?? []).Concat(rows.Select(row => row.Allocation)).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var generations = new HashSet<ulong>();
        var receipts = new Dictionary<string, OperationReservation>(StringComparer.Ordinal);
        ulong previousLast = current.HistoricalFloor, previousGeneration = 0;
        foreach (var row in operations.OrderBy(row => row.First))
        {
            if (!ids.Add(row.OperationId) || !receipts.TryAdd(row.OperationId, row) ||
                row.First <= previousLast || (ulong)row.First + (uint)row.Count - 1 > current.HighWater ||
                !generations.Add(row.Generation) || row.Generation <= previousGeneration || row.Generation > current.Generation)
                throw new JsonException("UID active/archive allocation ownership overlaps.");
            previousLast = (ulong)row.First + (uint)row.Count - 1;
            previousGeneration = row.Generation;
        }
        ids.Clear();
        foreach (var row in intents)
        {
            if (!ids.Add(row.OperationId) || !generations.Add(row.PreparedGeneration) || row.PreparedGeneration > current.Generation)
                throw new JsonException("UID active/archive producer identity or generation overlaps.");
            if (receipts.TryGetValue(row.OperationId, out var receipt) &&
                (receipt.Generation <= row.PreparedGeneration || receipt.Writer != row.Writer ||
                 receipt.AccountId != row.AccountId || receipt.CharacterUid != row.CharacterUid ||
                 receipt.PayloadSha256 != row.PayloadSha256 || receipt.Count != row.Count))
                throw new JsonException("UID archived payload and receipt differ.");
        }
        foreach (var row in rows)
            if (!generations.Add(row.AfterGeneration) || row.AfterGeneration > current.Generation)
                throw new JsonException("UID retirement generation overlaps an allocation or preparation.");
    }

    private LegacyItemUidProducerIntentSnapshot ArchiveSnapshot(ProducerArchive row) => ProducerSnapshot(row.Intent, row.Allocation);

    // Full closed membership and cross-set validation happens at quiescent open
    // and retirement. The real lease retains only immutable identity/hash data.
    // Unrelated injected history children are detected at the next open/retirement;
    // requested files are revalidated directly, and root membership stays hot.
    private void RememberArchiveIndex(RegistryState current, ArchiveCatalog catalog) => archiveIndex =
        new(current.ArchiveHead, current.ArchiveHeadSha256, current.ArchiveCount,
            catalog.Entries.ToDictionary(pair => pair.Key, pair => new ArchiveIdentity(pair.Value.Sha256,
                pair.Value.Record.Intent.AccountId, pair.Value.Record.Intent.CharacterUid,
                pair.Value.Record.Intent.PreparedGeneration), StringComparer.Ordinal));

    private ArchiveIdentityIndex CurrentArchives => archiveIndex is { } index && index.Head == state.ArchiveHead &&
        index.HeadSha256 == state.ArchiveHeadSha256 && index.Count == state.ArchiveCount &&
        (index.Count.GetValueOrDefault() == 0 || Directory.Exists(paths.History))
        ? index : throw new JsonException("UID archive identity index differs from live state.");

    private bool TryReadArchivedIntent(string operationId, out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason)
    {
        snapshot = null; reason = string.Empty;
        try
        {
            if (!CurrentArchives.Entries.TryGetValue(operationId, out var identity)) return true;
            var entry = ReadArchive(ArchivePath(paths, operationId), state);
            if (entry.Record.OperationId != operationId || entry.Sha256 != identity.Sha256)
                throw new JsonException("UID archived operation differs from its admitted hash.");
            snapshot = ArchiveSnapshot(entry.Record);
            return true;
        }
        catch (Exception error) when (IsFileFailure(error))
        { reason = RecoveryRequired + ":archiveRead:" + error.GetType().Name; blocked = reason; return false; }
    }

    private bool ReplayArchivedIntent(LegacyItemUidProducerIntentSnapshot archived, LegacyItemUidReservationRequest request,
        byte[] payload, out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason)
    {
        snapshot = null; reason = "itemUidProducerIntentConflict";
        if (archived.Request != request || !archived.CopyPayload().AsSpan().SequenceEqual(payload)) return false;
        snapshot = archived; reason = string.Empty; return true;
    }
}
