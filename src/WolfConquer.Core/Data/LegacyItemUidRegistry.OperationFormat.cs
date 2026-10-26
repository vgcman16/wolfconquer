using System.Text.Json;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private static readonly string[] OperationKeys = ["OperationId", "Writer", "AccountId", "CharacterUid",
        "PayloadSha256", "Generation", "First", "Count"];

    private static void ValidateOperationJson(JsonElement root, int version)
    {
        if (version == 1) return;
        if (!root.TryGetProperty("Operations", out var operations) || operations.ValueKind != JsonValueKind.Array ||
            operations.GetArrayLength() > MaximumOperationReceipts ||
            version == 2 && operations.GetArrayLength() == 0)
            throw new JsonException("Invalid UID operation receipt count.");
        foreach (var operation in operations.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid UID operation receipt.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in operation.EnumerateObject())
                if (!OperationKeys.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                    throw new JsonException("Unknown or duplicate UID operation property.");
            if (names.Count != OperationKeys.Length) throw new JsonException("Missing UID operation property.");
        }
    }

    private static void ValidateOperationState(RegistryState state)
    {
        if (state.Version == 1)
        {
            if (state.Operations is not null) throw new JsonException("Legacy UID state contains operations.");
            return;
        }
        if (state.Operations is null || state.Operations.Length > MaximumOperationReceipts ||
            state.Version == 2 && state.Operations.Length == 0)
            throw new JsonException("Missing UID operation receipts.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        ulong previousGeneration = 0;
        ulong previousLast = state.HistoricalFloor;
        foreach (var row in state.Operations)
        {
            if (row is null || !Guid.TryParseExact(row.OperationId, "N", out var operation) || operation == Guid.Empty ||
                operation.ToString("N") != row.OperationId || !identities.Add(row.OperationId) ||
                !IsWriterName(row.Writer) || row.AccountId == 0 || row.CharacterUid == 0 || !IsDigest(row.PayloadSha256) ||
                row.Count is < 1 or > MaximumReservation || row.First == 0 || row.First <= previousLast ||
                (ulong)row.First + (uint)row.Count - 1 > state.HighWater ||
                row.Generation <= previousGeneration || row.Generation > state.Generation)
                throw new JsonException("Invalid or overlapping UID operation receipt.");
            previousGeneration = row.Generation;
            previousLast = (ulong)row.First + (uint)row.Count - 1;
        }
    }
}
