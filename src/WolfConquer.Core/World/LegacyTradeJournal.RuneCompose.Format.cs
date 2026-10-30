using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRuneComposeOperation ReadRuneCompose(JsonElement element)
    {
        var fields = GetProperties(element, "SelectedUid", "Materials", "Metadata", "Progress", "MaterialMetadata");
        static uint ReadUInt(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number)
            ? number : throw new JsonException("Invalid Rune compose unsigned input.");
        static bool ReadBoolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : throw new JsonException("Invalid Rune compose boolean input.");
        var materials = fields["Materials"];
        if (materials.ValueKind != JsonValueKind.Array || materials.GetArrayLength() > 49)
            throw new JsonException("Invalid Rune compose material count.");
        var array = fields["Metadata"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > ushort.MaxValue)
            throw new JsonException("Invalid Rune compose metadata count.");
        var metadata = new List<LegacyRuneMetadata>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "ItemId", "RuneSpellId", "RequiredClass", "RequiredLevel", "IsRuneSkill");
            metadata.Add(new(ReadUInt(row["ItemId"]), ReadUInt(row["RuneSpellId"]), ReadUInt(row["RequiredClass"]), ReadUInt(row["RequiredLevel"]),
                row["IsRuneSkill"].ValueKind == JsonValueKind.Null ? null : ReadBoolean(row["IsRuneSkill"])));
        }
        array = fields["Progress"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 128)
            throw new JsonException("Invalid Rune compose progress count.");
        var progress = new List<LegacyRuneComposeProgressRow>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "Family", "Level", "Progress");
            progress.Add(new(ReadUInt(row["Family"]), checked((byte)ReadUInt(row["Level"])), ReadUInt(row["Progress"])));
        }
        array = fields["MaterialMetadata"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 49)
            throw new JsonException("Invalid Rune compose material metadata count.");
        var materialMetadata = new List<LegacyRuneComposeMaterialMetadata>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "ItemId", "MaximumStackSize");
            materialMetadata.Add(new(ReadUInt(row["ItemId"]), checked((ushort)ReadUInt(row["MaximumStackSize"]))));
        }
        var operation = new LegacyRuneComposeOperation(ReadUInt(fields["SelectedUid"]),
            materials.EnumerateArray().Select(ReadUInt).ToArray(), metadata.ToArray(), progress.ToArray(), materialMetadata.ToArray());
        operation.Validate(); return operation;
    }
}
