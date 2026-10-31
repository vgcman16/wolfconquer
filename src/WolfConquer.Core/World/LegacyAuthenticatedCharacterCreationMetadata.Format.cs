using System.Text.Json;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyAuthenticatedCharacterCreationMetadata
{
    internal static LegacyAuthenticatedCharacterCreationMetadata Read(JsonElement element)
    {
        var root = Properties(element, "Items", "Profession", "RuneStorageZeroScore", "InnerPowerRows");
        var itemArray = root["Items"];
        if (itemArray.ValueKind != JsonValueKind.Array || itemArray.GetArrayLength() is < 1 or > 4)
            throw new JsonException("Invalid creation item metadata count.");
        var items = itemArray.EnumerateArray().Select(item =>
        {
            var row = Properties(item, "ItemId", "RawHitpoints", "RawDurability");
            if (row["ItemId"].ValueKind != JsonValueKind.Number || !row["ItemId"].TryGetUInt32(out var id)) throw new JsonException("Invalid creation item ID.");
            return new LegacyAuthenticatedCharacterCreationItemMetadata(id, Text(row["RawHitpoints"]), Text(row["RawDurability"]));
        }).ToArray();
        var profession = root["Profession"].ValueKind == JsonValueKind.Null ? null : Fields(root["Profession"]);
        var runes = root["RuneStorageZeroScore"];
        if (runes.ValueKind != JsonValueKind.Array || runes.GetArrayLength() > 128) throw new JsonException("Invalid creation Rune dependencies.");
        var inner = root["InnerPowerRows"];
        if (inner.ValueKind != JsonValueKind.Array || inner.GetArrayLength() != 0) throw new JsonException("Fresh creation InnerPower must be absent.");
        return new(items, profession, runes.EnumerateArray().Select(Fields).ToArray(), []);
    }

    private static string[] Fields(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 9) throw new JsonException("Invalid creation table field count.");
        return value.EnumerateArray().Select(Text).ToArray();
    }
    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: <= 64 } text
        ? text : throw new JsonException("Invalid creation table text.");
    private static Dictionary<string, JsonElement> Properties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Creation metadata object is missing.");
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !values.TryAdd(property.Name, property.Value))
                throw new JsonException("Unknown or duplicate creation metadata property.");
        if (values.Count != names.Length) throw new JsonException("Incomplete creation metadata.");
        return values;
    }
}
