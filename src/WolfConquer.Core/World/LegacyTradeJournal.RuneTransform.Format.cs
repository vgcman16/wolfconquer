using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRuneTransformDependency ReadRuneTransformDependency(JsonElement element)
    {
        var fields = GetProperties(element, "Fields");
        var array = fields["Fields"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 8)
            throw new JsonException("Invalid captured Rune transform field count.");
        var values = new List<LegacyRuneTransformField>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "Key", "Value");
            if (row["Key"].ValueKind != JsonValueKind.String || row["Value"].ValueKind != JsonValueKind.String)
                throw new JsonException("Captured Rune transform field requires exact key/value text.");
            values.Add(new(row["Key"].GetString()!, row["Value"].GetString()!));
        }
        var captured = new LegacyRuneTransformDependency(values.ToArray());
        captured.Validate(); return captured;
    }
}
