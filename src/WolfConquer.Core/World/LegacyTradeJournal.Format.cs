using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static byte[] Serialize(JournalRecord record)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record);
        if (bytes.Length > MaxJournalBytes) throw new ArgumentException("Journal is too large.");
        return bytes;
    }

    private static JournalRecord ReadRecord(string path)
    {
        EnsureSafePath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaxJournalBytes) throw new JsonException("Invalid journal size.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        var rootElement = document.RootElement;
        var candidateVersion = 0;
        if (rootElement.ValueKind == JsonValueKind.Object &&
            rootElement.TryGetProperty("Version", out var candidate) && candidate.ValueKind == JsonValueKind.Number)
            candidate.TryGetInt32(out candidateVersion);
        var root = candidateVersion switch
        {
            3 => GetProperties(rootElement, "Version", "TransactionId", "Changes", "Domain"),
            4 => GetProperties(rootElement, "Version", "TransactionId", "Changes", "MailboxDomain"),
            5 => GetProperties(rootElement, "Version", "TransactionId", "Changes", "MailboxGearDomain"),
            6 => GetProperties(rootElement, "Version", "TransactionId", "Changes", "AdmissionDomain", "AdmissionInputs"),
            7 => GetProperties(rootElement, "Version", "TransactionId", "Changes", "AdmissionDomain", "AdmissionInputs", "AdmissionPreparedAt"),
            8 => GetProperties(rootElement, "Version", "TransactionId", "Changes", "CharacterSpellsDomain"),
            9 => GetProperties(rootElement, "Version", "TransactionId", "Changes", "CharacterItemsSpellsDomain"),
            _ => GetProperties(rootElement, "Version", "TransactionId", "Changes"),
        };
        if (root["Version"].ValueKind != JsonValueKind.Number ||
            !root["Version"].TryGetInt32(out var version) || version is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9) ||
            root["TransactionId"].ValueKind != JsonValueKind.String)
            throw new JsonException("Unsupported journal version or identity.");
        var transactionId = root["TransactionId"].GetString();
        if (!Guid.TryParseExact(transactionId, "N", out var id) || id == Guid.Empty)
            throw new JsonException("Invalid transaction identity.");
        var array = root["Changes"];
        if (array.ValueKind != JsonValueKind.Array ||
            (IsOrdinaryAdmissionVersion(version) ? array.GetArrayLength() is < 1 or > 4 : array.GetArrayLength() != TargetCount(version)))
            throw new JsonException("Invalid target count.");
        var changes = new List<LegacyTradeJournalChange>(TargetCount(version));
        foreach (var element in array.EnumerateArray())
        {
            var entry = GetProperties(element, "Kind", "FileId", "Before", "After");
            if (entry["Kind"].ValueKind != JsonValueKind.Number || entry["FileId"].ValueKind != JsonValueKind.Number ||
                !entry["Kind"].TryGetInt32(out var kind) || !entry["FileId"].TryGetInt64(out var fileId))
                throw new JsonException("Invalid target identity.");
            changes.Add(new((LegacyTradeJournalFileKind)kind, fileId,
                ReadBytes(entry["Before"], allowNull: true), ReadBytes(entry["After"], allowNull: false)!));
        }
        var result = changes.ToArray();
        ValidateChanges(result, version);
        if (version == 5) RequireMailboxGearOrder(result);
        if (version == 8) RequireCharacterSpellsOrder(result);
        if (version == 9) RequireCharacterItemsSpellsOrder(result);
        return new(version, transactionId!, result)
        {
            Domain = version == 3 ? ReadQuestDomain(root["Domain"]) : null,
            MailboxDomain = version == 4 ? ReadMailboxDomain(root["MailboxDomain"]) : null,
            MailboxGearDomain = version == 5 ? ReadMailboxGearDomain(root["MailboxGearDomain"]) : null,
            AdmissionDomain = IsOrdinaryAdmissionVersion(version) ? ReadAdmissionDomain(root["AdmissionDomain"]) : null,
            AdmissionInputs = IsOrdinaryAdmissionVersion(version) ? ReadAdmissionInputs(root["AdmissionInputs"]) : null,
            AdmissionPreparedAt = version == 7 ? ReadAdmissionTime(root["AdmissionPreparedAt"]) : null,
            CharacterSpellsDomain = version == 8 ? ReadCharacterSpellsDomain(root["CharacterSpellsDomain"]) : null,
            CharacterItemsSpellsDomain = version == 9 ? ReadCharacterItemsSpellsDomain(root["CharacterItemsSpellsDomain"]) : null,
        };
    }

    private static Dictionary<string, JsonElement> GetProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected an object.");
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !properties.TryAdd(property.Name, property.Value))
                throw new JsonException("Unknown or duplicate property.");
        if (properties.Count != names.Length) throw new JsonException("Missing property.");
        return properties;
    }

    private static byte[]? ReadBytes(JsonElement element, bool allowNull)
    {
        if (allowNull && element.ValueKind == JsonValueKind.Null) return null;
        if (element.ValueKind != JsonValueKind.String || element.GetRawText().Length > ((MaxTargetBytes + 2) / 3) * 4 + 2)
            throw new JsonException("Invalid image encoding or size.");
        var bytes = element.GetBytesFromBase64();
        if (bytes.Length > MaxTargetBytes) throw new JsonException("Image is too large.");
        return bytes;
    }
}
