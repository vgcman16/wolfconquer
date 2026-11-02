using System.Text.Json;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private const int MaximumBytes = 16 * 1024;
    private static readonly string[] Keys = ["Version", "Domain", "Operation", "PayloadSha256", "AccountId", "CharacterUid",
        "First", "Count", "Users", "Items", "Mailbox", "CombatGear", "Before", "After", "Completed"];
    private static readonly string[] NoAllocationKeys = ["AllocationKind", "PayloadBase64"];

    private Record ParseRecord(byte[] bytes, Guid operation)
    {
        if (bytes.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Package work size is invalid.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 3 });
        return ParseRecordElement(document.RootElement, bytes, operation);
    }

    // The enclosing document already owns the element. Retain the exact
    // canonical-byte limit and depth check without allocating another DOM.
    private Record ParseEmbeddedRecord(JsonElement element, Guid operation)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(element);
        if (bytes.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Package work size is invalid.");
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 3 });
        while (reader.Read()) { }
        return ParseRecordElement(element, bytes, operation);
    }

    private Record ParseRecordElement(JsonElement element, byte[] bytes, Guid operation)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Package work is not an object.");
        if (!element.TryGetProperty("Version", out var versionValue) || versionValue.ValueKind != JsonValueKind.Number ||
            !versionValue.TryGetInt32(out var version) || version is not (1 or 2))
            throw new InvalidDataException("Package work version is invalid.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if ((!Keys.Contains(property.Name, StringComparer.Ordinal) &&
                !(version == 2 && NoAllocationKeys.Contains(property.Name, StringComparer.Ordinal))) || !names.Add(property.Name))
                throw new InvalidDataException("Package work properties are invalid.");
        if (names.Count != Keys.Length + (version == 2 ? NoAllocationKeys.Length : 0))
            throw new InvalidDataException("Package work properties are missing.");
        var row = JsonSerializer.Deserialize<Record>(bytes) ?? throw new InvalidDataException("Package work is missing.");
        if (row.Version != version || row.Domain != domain.ToString("N") || row.Operation != operation.ToString("N") ||
            row.AccountId == 0 || row.CharacterUid == 0 || !IsDigest(row.PayloadSha256) ||
            row.Users != users || row.Items != items || row.Mailbox != mailbox || row.CombatGear != gear ||
            row.Before is not { Length: 4 } || row.After is not { Length: 4 } ||
            row.Before.Any(hash => !IsDigest(hash)) || row.After.Any(hash => !IsDigest(hash)))
            throw new InvalidDataException("Package work bindings are invalid.");
        if (version == 1 ? row.First == 0 || row.Count is < 1 or > 40 ||
            (ulong)row.First + (uint)row.Count - 1 > uint.MaxValue || row.AllocationKind is not null || row.PayloadBase64 is not null
            : row.First != 0 || row.Count != 0 || row.AllocationKind != "NoAllocation" ||
                !owner.TryValidateNoAllocationRecord(row, out _))
            throw new InvalidDataException("Package allocation identity is invalid.");
        return row;
    }

    private static bool IsDigest(string? text) => text is { Length: 64 } &&
        text.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f');
}
