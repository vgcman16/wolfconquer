using System.Text.Json;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private sealed record CompletedEntry(int ArchiveVersion, string PreviousOperation, string PreviousSha256, Record Work);
    private string CompletedRoot => Path.Combine(directory, "completed");
    private string CompletedPath(Guid operation)
    {
        var name = operation.ToString("N");
        return Path.Combine(CompletedRoot, name[..2], name + ".json");
    }

    private (CompletedEntry Entry, string Hash)? ReadCompletedEntry(Guid operation)
    {
        var path = CompletedPath(operation);
        EnsureOrdinary(path);
        if (!File.Exists(path)) return null;
        var bytes = ReadHistoryBytes(path, MaximumBytes + 1024);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        RequireProperties(root, ["ArchiveVersion", "PreviousOperation", "PreviousSha256", "Work"]);
        var previous = root.GetProperty("PreviousOperation").GetString();
        var hash = root.GetProperty("PreviousSha256").GetString();
        if (root.GetProperty("ArchiveVersion").GetInt32() != 1 || !Guid.TryParseExact(previous, "N", out var prior) ||
            prior.ToString("N") != previous || !IsDigest(hash) ||
            (prior == Guid.Empty) != (hash == EmptyArchiveHash)) throw new InvalidDataException("Completed archive link is invalid.");
        var row = ParseRecord(JsonSerializer.SerializeToUtf8Bytes(root.GetProperty("Work")), operation);
        if (!row.Completed) throw new InvalidDataException("Archived work is not completed.");
        return (new(1, previous!, hash!, row), Digest(bytes));
    }

    private Record? ReadCompleted(Guid operation)
    {
        var actual = ReadCompletedEntry(operation);
        if (!completedHashes.TryGetValue(operation.ToString("N"), out var expected))
        {
            if (actual is not null) throw new InvalidDataException("Unanchored completed work exists.");
            return null;
        }
        if (actual is null || actual.Value.Hash != expected) throw new InvalidDataException("Anchored completed work is missing or changed.");
        return actual.Value.Entry.Work;
    }

    private void PutCompleted(Record row)
    {
        if (!row.Completed) throw new InvalidOperationException("Only completed work belongs in the archive.");
        RequireHistoryBinding(row);
        var index = ReadIndex();
        RequireKnownAnchor(index);
        var operation = Guid.ParseExact(row.Operation, "N");
        if (completedHashes.ContainsKey(row.Operation))
        {
            if (ReadCompleted(operation) is not { } existing || !Same(existing, row))
                throw new InvalidOperationException("Completed history conflicts with retained work.");
            foreach (var lineage in HistoryLineages(row)) PutLineage(row, lineage);
            return;
        }
        if (index.CompletedCount == ulong.MaxValue) throw new InvalidOperationException("Completed archive count is exhausted.");
        var entry = new CompletedEntry(1, index.CompletedHead, index.CompletedHeadSha256, row);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entry, HistoryJson);
        var path = CompletedPath(operation);
        var prior = ReadCompletedEntry(operation);
        if (prior is not null && (prior.Value.Hash != Digest(bytes) || !Same(prior.Value.Entry.Work, row)))
            throw new InvalidDataException("Unanchored completion conflicts with its exact predecessor.");
        if (prior is null) WriteHistoryAtomically(path, bytes, createOnly: true);
        foreach (var lineage in HistoryLineages(row)) PutLineage(row, lineage);
        var next = index with { CompletedHead = row.Operation, CompletedHeadSha256 = Digest(bytes), CompletedCount = index.CompletedCount + 1,
            Pending = index.Pending.Where(value => value.Operation != row.Operation).ToArray() };
        WriteIndex(next);
        completedHashes.Add(row.Operation, next.CompletedHeadSha256);
        foreach (var lineage in HistoryLineages(row))
            completedLineageOperations.Add(LineageName(row.AccountId, row.CharacterUid, lineage), row.Operation);
        RememberAnchor(next);
    }

    private string[] HistoryLineages(Record row)
    {
        byte[] payload;
        if (row.Version == 1)
        {
            if (!owner.Registry.TryFindProducerIntent(domain, Guid.ParseExact(row.Operation, "N"), out var intent, out var found, out _) ||
                !found || intent is null || !MatchesIntent(row, intent)) throw new InvalidDataException("Completed allocation history is unbound.");
            if (!row.Completed && (!owner.Registry.TryIsActiveProducerIntent(domain, Guid.ParseExact(row.Operation, "N"), out var active, out _) || !active))
                throw new InvalidDataException("Pending allocation work is not an active producer intent.");
            payload = intent.CopyPayload();
        }
        else
        {
            if (!owner.TryValidateNoAllocationRecord(row, out _)) throw new InvalidDataException("Completed no-allocation history is unbound.");
            payload = Convert.FromBase64String(row.PayloadBase64!);
        }
        if (!LegacyMailboxPackageRecipe.TryReadHeader(payload, domain, row.AccountId, row.CharacterUid, out var keys))
            throw new InvalidDataException("Completed history lineage is invalid.");
        return keys;
    }

    private void RequireHistoryBinding(Record row)
    {
        _ = HistoryLineages(row);
        if (row.Version == 2 && (!owner.Registry.TryFindProducerIntent(domain, Guid.ParseExact(row.Operation, "N"),
            out _, out var found, out _) || found)) throw new InvalidDataException("No-allocation history has a UID allocation.");
    }

    private IEnumerable<Record> EnumerateCompleted()
    {
        foreach (var operation in completedHashes.Keys)
        {
            var row = ReadCompleted(Guid.ParseExact(operation, "N")) ?? throw new IOException("Completed history disappeared.");
            RequireHistoryBinding(row);
            yield return row;
        }
    }
}
