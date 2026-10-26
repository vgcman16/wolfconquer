using System.Text.Json;
using System.Text.Json.Serialization;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private const int LegacyMaximumStateBytes = 64 * 1024;
    private const int MaximumStateBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions StateJsonOptions = new()
    { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly string[] StateKeys = ["Version", "Domain", "Users", "Items", "Mailbox", "Redeem", "Claim",
        "HistoricalFloor", "HistoricalEvidence", "Generation", "HighWater"];

    private static byte[] SerializeState(RegistryState state)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, StateJsonOptions);
        if (bytes.Length > MaximumStateBytes || state.Version == 1 && bytes.Length > LegacyMaximumStateBytes)
            throw new FormatException("UID state exceeds its supported size.");
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
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 5 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("UID state must be an object.");
        if (!document.RootElement.TryGetProperty("Version", out var versionElement) ||
            versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetInt32(out var version) || version is < 1 or > 2 ||
            version == 1 && bytes.Length > LegacyMaximumStateBytes)
            throw new JsonException("Unsupported UID state version or length.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if ((!StateKeys.Contains(property.Name, StringComparer.Ordinal) &&
                !(version == 2 && property.Name == "Operations")) || !names.Add(property.Name))
                throw new JsonException("Unknown or duplicate UID state property.");
        if (names.Count != StateKeys.Length + (version == 2 ? 1 : 0)) throw new JsonException("Missing UID state property.");
        ValidateOperationJson(document.RootElement, version);
        var state = JsonSerializer.Deserialize<RegistryState>(bytes) ?? throw new JsonException("Missing UID state.");
        if (!Guid.TryParseExact(state.Domain, "N", out var domain) || domain == Guid.Empty ||
            string.IsNullOrWhiteSpace(state.HistoricalEvidence) || state.HistoricalEvidence.Length > 4096 ||
            state.HighWater < state.HistoricalFloor ||
            new[] { state.Users, state.Items, state.Mailbox, state.Redeem, state.Claim }.Any(value =>
                string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value)))
            throw new JsonException("Invalid UID state values.");
        ValidateOperationState(state);
        return state;
    }
}
