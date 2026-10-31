using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRuneBagOperation ReadRuneBag(JsonElement element)
    {
        var fields = GetProperties(element, "IntoBag", "SelectedUid", "Metadata", "Request", "Allocation");
        if (fields["IntoBag"].ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new JsonException("RuneBag direction is not a boolean.");
        var metadata = GetProperties(fields["Metadata"], "ItemId", "RawMaximumStackSize");
        var result = new LegacyRuneBagOperation(fields["IntoBag"].GetBoolean(), ReadRuneDecomposeUInt(fields["SelectedUid"]),
            new(ReadRuneDecomposeUInt(metadata["ItemId"]), checked((ushort)ReadRuneDecomposeUInt(metadata["RawMaximumStackSize"]))),
            fields["Request"].ValueKind == JsonValueKind.Null ? null : ReadRuneDecomposeRequest(fields["Request"]),
            fields["Allocation"].ValueKind == JsonValueKind.Null ? null : ReadRuneDecomposeAllocation(fields["Allocation"]));
        result.Validate(); return result;
    }
}
