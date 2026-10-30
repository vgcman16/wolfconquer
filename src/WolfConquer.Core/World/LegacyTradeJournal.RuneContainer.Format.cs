using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRuneContainerOperation ReadRuneContainer(JsonElement element, bool current)
    {
        var fields = current ? GetProperties(element, "Kind", "SelectedUid", "Metadata", "AlternateEquipmentActive", "Transform") :
            GetProperties(element, "Kind", "SelectedUid", "Metadata", "AlternateEquipmentActive");
        static uint ReadUInt(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number)
            ? number : throw new JsonException("Invalid Rune container unsigned input.");
        static bool ReadBool(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : throw new JsonException("Invalid Rune container boolean input.");
        var kind = ReadUInt(fields["Kind"]);
        if (kind > int.MaxValue || !Enum.IsDefined((LegacyRuneContainerKind)kind)) throw new JsonException("Unknown Rune container kind.");
        var array = fields["Metadata"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > ushort.MaxValue)
            throw new JsonException("Invalid Rune container metadata count.");
        var metadata = new List<LegacyRuneMetadata>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "ItemId", "RuneSpellId", "RequiredClass", "RequiredLevel", "IsRuneSkill");
            metadata.Add(new(ReadUInt(row["ItemId"]), ReadUInt(row["RuneSpellId"]), ReadUInt(row["RequiredClass"]), ReadUInt(row["RequiredLevel"]),
                row["IsRuneSkill"].ValueKind == JsonValueKind.Null ? null : ReadBool(row["IsRuneSkill"])));
        }
        var operation = new LegacyRuneContainerOperation((LegacyRuneContainerKind)kind, ReadUInt(fields["SelectedUid"]),
            metadata.ToArray(), ReadBool(fields["AlternateEquipmentActive"]),
            current ? ReadRuneTransformDependency(fields["Transform"]) : null);
        operation.Validate(current ? LegacyRuneMappingSemantics.RawSelf : LegacyRuneMappingSemantics.LegacyDisplay); return operation;
    }
}
