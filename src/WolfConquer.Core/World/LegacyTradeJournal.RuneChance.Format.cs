using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRuneChanceOperation ReadRuneChance(JsonElement element)
    {
        var fields = GetProperties(element, "SelectedUid", "Metadata", "Progress", "Roll");
        static uint ReadUInt(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number)
            ? number : throw new JsonException("Invalid Rune chance unsigned input.");
        static bool ReadBoolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : throw new JsonException("Invalid Rune chance boolean input.");
        var array = fields["Metadata"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > ushort.MaxValue)
            throw new JsonException("Invalid Rune chance metadata count.");
        var metadata = new List<LegacyRuneMetadata>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "ItemId", "RuneSpellId", "RequiredClass", "RequiredLevel", "IsRuneSkill");
            metadata.Add(new(ReadUInt(row["ItemId"]), ReadUInt(row["RuneSpellId"]), ReadUInt(row["RequiredClass"]), ReadUInt(row["RequiredLevel"]),
                row["IsRuneSkill"].ValueKind == JsonValueKind.Null ? null : ReadBoolean(row["IsRuneSkill"])));
        }
        array = fields["Progress"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 2)
            throw new JsonException("Invalid Rune chance progress count.");
        var progress = new List<LegacyRuneChanceProgressRow>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "Family", "Level", "Progress");
            progress.Add(new(ReadUInt(row["Family"]), checked((byte)ReadUInt(row["Level"])), ReadUInt(row["Progress"])));
        }
        byte? roll = fields["Roll"].ValueKind == JsonValueKind.Null ? null : checked((byte)ReadUInt(fields["Roll"]));
        var operation = new LegacyRuneChanceOperation(ReadUInt(fields["SelectedUid"]), metadata.ToArray(), progress.ToArray(), roll);
        operation.Validate(); return operation;
    }
}
