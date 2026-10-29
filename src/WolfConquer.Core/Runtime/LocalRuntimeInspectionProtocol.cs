using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

public static partial class LocalRuntimeInspectionProtocol
{
    public const int MaximumRequestBytes = 1024, MaximumResponseBytes = 256 * 1024, ReceiptLength = 20;
    private const string Prefix = "wolfconquer-runtime-inspect-";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true, MaxDepth = 8,
        Converters = { new JsonStringEnumConverter<LegacyRuntimeInspectionStatus>(allowIntegerValues: false) }
    };
    public static string NameFor(Guid domain) => domain == Guid.Empty ? throw new ArgumentException("domainRequired") : Prefix + domain.ToString("N");
    public static bool IsPipeName(string name) => name is not null && name.Length == Prefix.Length + 32 &&
        name.StartsWith(Prefix, StringComparison.Ordinal) && Guid.TryParseExact(name.AsSpan(Prefix.Length), "N", out var domain) &&
        domain != Guid.Empty && name == NameFor(domain);

    public static LegacyRuntimeInspectionRequest ParseRequest(ReadOnlyMemory<byte> bytes)
    {
        using var document = Document(bytes, MaximumRequestBytes);
        var value = document.RootElement.Deserialize<LegacyRuntimeInspectionRequest>(Json) ?? throw new JsonException("requestRequired");
        if (value.SchemaVersion != 1 || value.QueryId == Guid.Empty || value.ExpectedDomain == Guid.Empty || value.AccountId == 0 || value.CharacterUid == 0)
            throw new JsonException("requestIdentityRequired");
        return value;
    }
    public static byte[] RequestBytes(LegacyRuntimeInspectionRequest request)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, Json); _ = ParseRequest(bytes); return bytes;
    }
    public static string FormatResult(LegacyRuntimeInspectionResult result) => JsonSerializer.Serialize(result, Json);
    public static LegacyRuntimeInspectionResult ParseResult(ReadOnlyMemory<byte> bytes)
    {
        using var document = Document(bytes, MaximumResponseBytes); var root = document.RootElement;
        if (!root.TryGetProperty("readOnly", out var readOnly) || readOnly.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("atomicNativeTupleProven", out var atomic) || atomic.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("liveStoreSynchronized", out var live) || live.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("runtimeObserved", out var observed) || observed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new JsonException("diagnosticProofFlagsRequired");
        var value = root.Deserialize<LegacyRuntimeInspectionResult>(Json) ?? throw new JsonException("resultRequired");
        if (value.SchemaVersion != 1 || string.IsNullOrWhiteSpace(value.Reason) || value.Reason.Length > 256 ||
            !Enum.IsDefined(value.Status) || observed.GetBoolean() != value.RuntimeObserved ||
            (value.Status == LegacyRuntimeInspectionStatus.Observed) != (value.Snapshot is not null)) throw new JsonException("resultShapeInvalid");
        if (value.Snapshot is { } s)
        {
            if (value.QueryId == Guid.Empty || s.Domain == Guid.Empty || s.AccountId == 0 || s.CharacterUid == 0 ||
                s.ProtocolProfile != "VisibleDx8D13" || s.ManagedCharacter.Uid != s.CharacterUid || s.NativeVitals.Uid != s.CharacterUid ||
                s.ManagedCharacter.Hitpoints is null || s.ManagedCharacter.MinMana is null || s.ManagedCharacter.MaxMana is null)
                throw new JsonException("snapshotIdentityOrVitalsInvalid");
            RequireCanonicalRows(s.ManagedItems);
            RequireNativeRowsAndComparison(s);
        }
        return value;
    }
    public static byte[] ResponseReceipt(Guid query)
    {
        var bytes = new byte[ReceiptLength]; "WRI1"u8.CopyTo(bytes); query.TryWriteBytes(bytes.AsSpan(4)); return bytes;
    }
    private static JsonDocument Document(ReadOnlyMemory<byte> bytes, int maximum)
    {
        if (bytes.Length is 0 || bytes.Length > maximum) throw new JsonException("messageLengthInvalid");
        var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        try
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("objectRequired");
            Duplicates(doc.RootElement); return doc;
        }
        catch { doc.Dispose(); throw; }
    }
    private static void Duplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) throw new JsonException("duplicateProperty"); Duplicates(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) Duplicates(child);
    }
}
