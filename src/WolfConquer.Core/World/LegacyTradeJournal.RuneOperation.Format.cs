using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRuneOperation ReadRuneOperation(JsonElement element, bool current)
    {
        var fields = current
            ? GetProperties(element, "Equip", "SelectedUid", "RequestedPosition", "Metadata", "AlternateEquipmentActive",
                "BeastLevel", "BeastFlags", "BeastYellowSlots", "Transform")
            : GetProperties(element, "Equip", "SelectedUid", "RequestedPosition", "Metadata", "AlternateEquipmentActive",
                "BeastLevel", "BeastFlags", "BeastYellowSlots");
        static bool ReadBoolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : throw new JsonException("Invalid Rune boolean input.");
        static uint ReadUInt(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number)
            ? number : throw new JsonException("Invalid Rune unsigned input.");
        var array = fields["Metadata"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > ushort.MaxValue)
            throw new JsonException("Invalid recorded Rune metadata count.");
        var metadata = new List<LegacyRuneMetadata>();
        foreach (var entry in array.EnumerateArray())
        {
            var row = GetProperties(entry, "ItemId", "RuneSpellId", "RequiredClass", "RequiredLevel", "IsRuneSkill");
            metadata.Add(new(ReadUInt(row["ItemId"]), ReadUInt(row["RuneSpellId"]), ReadUInt(row["RequiredClass"]), ReadUInt(row["RequiredLevel"]),
                row["IsRuneSkill"].ValueKind == JsonValueKind.Null ? null : ReadBoolean(row["IsRuneSkill"])));
        }
        var operation = new LegacyRuneOperation(ReadBoolean(fields["Equip"]), ReadUInt(fields["SelectedUid"]),
            checked((ushort)ReadUInt(fields["RequestedPosition"])), metadata.ToArray(), ReadBoolean(fields["AlternateEquipmentActive"]),
            checked((byte)ReadUInt(fields["BeastLevel"])), ReadUInt(fields["BeastFlags"]),
            fields["BeastYellowSlots"].ValueKind == JsonValueKind.Null ? null : checked((byte)ReadUInt(fields["BeastYellowSlots"])),
            current ? ReadRuneTransformDependency(fields["Transform"]) : null);
        operation.Validate(current ? LegacyRuneMappingSemantics.RawSelf : LegacyRuneMappingSemantics.LegacyDisplay); return operation;
    }
}
