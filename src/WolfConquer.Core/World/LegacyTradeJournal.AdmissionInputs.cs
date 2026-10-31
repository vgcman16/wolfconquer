using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void EnsureAdmissionStagesCleared(JournalRecord record, JournalTarget[] targets)
    {
        if (!IsOrdinaryAdmissionVersion(record.Version) && record.Version is not (8 or 9 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19)) return;
        foreach (var target in targets)
            if (!TryDelete(StagePath(target, record.TransactionId)))
                throw new IOException(record.Version is 9 or 12 or 13 or 14 or 15 or 16 or 17 or 18
                    ? "Character-items-and-spells staging cleanup is incomplete."
                    : record.Version == 8 ? "Character-and-spells staging cleanup is incomplete."
                    : "Ordinary-admission staging cleanup is incomplete.");
    }

    private static LegacyOrdinaryAdmissionInput[] ReadAdmissionInputs(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 5)
            throw new JsonException("Invalid admission input count.");
        var inputs = new List<LegacyOrdinaryAdmissionInput>();
        foreach (var row in element.EnumerateArray())
        {
            var values = GetProperties(row, "Kind", "FileId", "Image");
            if (values["Kind"].ValueKind != JsonValueKind.Number || values["FileId"].ValueKind != JsonValueKind.Number ||
                !values["Kind"].TryGetInt32(out var kind) || !values["FileId"].TryGetInt64(out var id) || id <= 0)
                throw new JsonException("Invalid admission input identity.");
            inputs.Add(new((LegacyTradeJournalFileKind)kind, id, ReadBytes(values["Image"], true)));
        }
        return inputs.ToArray();
    }

    private static bool TryRollbackWithAdmissionInputs(JournalRoots roots, JournalRecord record, JournalTarget[] targets)
    {
        try { EnsureAdmissionInputs(roots, record); RequireAuthenticatedCreationBoundary(record); return TryRollback(targets, record.TransactionId); }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }
}
