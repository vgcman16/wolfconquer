using System.Security.Cryptography;
using System.Text.Json;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private static readonly string[] ProducerIntentKeys = ["OperationId", "Writer", "AccountId", "CharacterUid",
        "PayloadSha256", "Count", "PayloadBase64", "PreparedGeneration"];

    private static void ValidateProducerIntentJson(JsonElement root, int version)
    {
        if (version != 3) return;
        if (!root.TryGetProperty("ProducerIntents", out var intents) || intents.ValueKind != JsonValueKind.Array ||
            intents.GetArrayLength() is < 1 or > MaximumProducerIntents)
            throw new JsonException("Invalid UID producer intent count.");
        foreach (var intent in intents.EnumerateArray())
        {
            if (intent.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid UID producer intent.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in intent.EnumerateObject())
                if (!ProducerIntentKeys.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                    throw new JsonException("Unknown or duplicate UID producer property.");
            if (names.Count != ProducerIntentKeys.Length) throw new JsonException("Missing UID producer property.");
        }
    }

    private static void ValidateProducerIntentState(RegistryState state)
    {
        if (state.Version != 3)
        {
            if (state.ProducerIntents is not null) throw new JsonException("Legacy state contains producer intents.");
            return;
        }
        if (state.ProducerIntents is not { Length: >= 1 and <= MaximumProducerIntents })
            throw new JsonException("Missing UID producer intents.");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var generations = state.Operations!.Select(row => row.Generation).ToHashSet();
        ulong previousGeneration = 0;
        var totalBytes = 0;
        foreach (var row in state.ProducerIntents)
        {
            if (row is null || !Guid.TryParseExact(row.OperationId, "N", out var id) || id == Guid.Empty ||
                id.ToString("N") != row.OperationId || !identities.Add(row.OperationId) || !IsWriterName(row.Writer) ||
                row.AccountId == 0 || row.CharacterUid == 0 || !IsDigest(row.PayloadSha256) ||
                row.Count is < 1 or > MaximumReservation || row.PreparedGeneration <= previousGeneration ||
                row.PreparedGeneration > state.Generation || !generations.Add(row.PreparedGeneration) ||
                !TryDecodeProducerPayload(row, out var payload))
                throw new JsonException("Invalid UID producer binding, payload or generation.");
            totalBytes += payload.Length;
            if (totalBytes > MaximumTotalProducerPayloadBytes) throw new JsonException("Producer payload total exceeds budget.");
            previousGeneration = row.PreparedGeneration;
            var allocation = state.Operations!.FirstOrDefault(operation => operation.OperationId == row.OperationId);
            if (allocation is not null && (allocation.Generation <= row.PreparedGeneration ||
                allocation.Writer != row.Writer || allocation.AccountId != row.AccountId ||
                allocation.CharacterUid != row.CharacterUid || allocation.PayloadSha256 != row.PayloadSha256 ||
                allocation.Count != row.Count)) throw new JsonException("UID allocation differs from its earlier producer intent.");
        }
    }

    private static bool TryDecodeProducerPayload(ProducerIntent intent, out byte[] payload)
    {
        payload = [];
        if (intent.PayloadBase64 is not { Length: >= 4 } ||
            intent.PayloadBase64.Length > ((MaximumProducerPayloadBytes + 2) / 3) * 4) return false;
        try { payload = Convert.FromBase64String(intent.PayloadBase64); }
        catch (FormatException) { return false; }
        return payload.Length is >= 1 and <= MaximumProducerPayloadBytes &&
            Convert.ToBase64String(payload) == intent.PayloadBase64 &&
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant() == intent.PayloadSha256;
    }
}
