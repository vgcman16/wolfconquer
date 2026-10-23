using System.Text.Json;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private const int MaximumStateBytes = 64 * 1024;
    private static readonly string[] StateKeys = ["Version", "Domain", "Users", "Items", "Mailbox", "Redeem", "Claim",
        "HistoricalFloor", "HistoricalEvidence", "Generation", "HighWater"];

    private static byte[] SerializeState(RegistryState state)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state);
        if (bytes.Length > MaximumStateBytes) throw new FormatException("UID state exceeds its supported size.");
        return bytes;
    }

    private static RegistryState ReadState(string path) => ParseState(ReadBounded(path));

    private static byte[] ReadBounded(string path)
    {
        EnsureOrdinaryPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumStateBytes) throw new FormatException("Invalid UID state length.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || stream.Length != bytes.Length) throw new IOException("UID state changed length.");
        return bytes;
    }

    private static RegistryState ParseState(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 3 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("UID state must be an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if (!StateKeys.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                throw new JsonException("Unknown or duplicate UID state property.");
        if (names.Count != StateKeys.Length) throw new JsonException("Missing UID state property.");
        var state = JsonSerializer.Deserialize<RegistryState>(bytes) ?? throw new JsonException("Missing UID state.");
        if (state.Version != 1 || !Guid.TryParseExact(state.Domain, "N", out var domain) || domain == Guid.Empty ||
            string.IsNullOrWhiteSpace(state.HistoricalEvidence) || state.HistoricalEvidence.Length > 4096 ||
            state.HighWater < state.HistoricalFloor ||
            new[] { state.Users, state.Items, state.Mailbox, state.Redeem, state.Claim }.Any(value =>
                string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)))
            throw new JsonException("Invalid UID state values.");
        return state;
    }
}
