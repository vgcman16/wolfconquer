using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyPhysicalDeathOperation ReadPhysicalDeath(JsonElement element)
    {
        var fields = GetProperties(element, "TargetUid", "QueryAdmitted", "Alternate", "ItemsBefore",
            "RankingRows", "DerivedSpells", "GrantedRows");
        if (fields["TargetUid"].ValueKind != JsonValueKind.Number || !fields["TargetUid"].TryGetUInt32(out var uid)) throw InvalidPhysicalDeathJson();
        var ranks = ReadPhysicalDeathArray(fields["RankingRows"], ushort.MaxValue).Select(row =>
        {
            var values = GetProperties(row, "CharacterId", "Fields");
            if (values["CharacterId"].ValueKind != JsonValueKind.Number || !values["CharacterId"].TryGetInt32(out var id)) throw InvalidPhysicalDeathJson();
            return new LegacyPhysicalDeathRankInput(id, ReadPhysicalDeathFields(values["Fields"]));
        }).ToArray();
        var changes = ReadPhysicalDeathArray(fields["DerivedSpells"], 2).Select(row =>
        {
            var values = GetProperties(row, "Id", "After");
            ushort id = ReadPhysicalDeathUShort(values["Id"]);
            PlayerSpellSnapshotEntry? after = null;
            if (values["After"].ValueKind != JsonValueKind.Null)
            {
                var spell = GetProperties(values["After"], "Id", "Level", "Experience", "PreviousLevel", "SoulLevel", "UseSpellSoul");
                if (spell["Experience"].ValueKind != JsonValueKind.Number || !spell["Experience"].TryGetInt32(out var experience)) throw InvalidPhysicalDeathJson();
                after = new(ReadPhysicalDeathUShort(spell["Id"]), ReadPhysicalDeathUShort(spell["Level"]), experience,
                    checked((byte)ReadPhysicalDeathUShort(spell["PreviousLevel"])), checked((byte)ReadPhysicalDeathUShort(spell["SoulLevel"])),
                    checked((byte)ReadPhysicalDeathUShort(spell["UseSpellSoul"])));
            }
            return new WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange(id, after);
        }).ToArray();
        var granted = ReadPhysicalDeathArray(fields["GrantedRows"], 2).Select(row =>
        {
            var values = GetProperties(row, "Id", "TypeId", "Name", "Fields");
            if (values["Id"].ValueKind != JsonValueKind.Number || values["TypeId"].ValueKind != JsonValueKind.Number ||
                !values["Id"].TryGetInt32(out var id) || !values["TypeId"].TryGetInt32(out var type)) throw InvalidPhysicalDeathJson();
            return new MagicType(id, type, ReadPhysicalDeathString(values["Name"]), ReadPhysicalDeathFields(values["Fields"]));
        }).ToArray();
        return new(uid, ReadPhysicalDeathBoolean(fields["QueryAdmitted"]), ReadPhysicalDeathBoolean(fields["Alternate"]),
            ReadBytes(fields["ItemsBefore"], allowNull: false)!, ranks, changes, granted);
    }

    private static JsonElement.ArrayEnumerator ReadPhysicalDeathArray(JsonElement value, int maximum)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > maximum) throw InvalidPhysicalDeathJson();
        return value.EnumerateArray();
    }

    private static string[] ReadPhysicalDeathFields(JsonElement value) =>
        ReadPhysicalDeathArray(value, 512).Select(ReadPhysicalDeathString).ToArray();

    private static string ReadPhysicalDeathString(JsonElement value) => value.ValueKind == JsonValueKind.String &&
        value.GetString() is { Length: <= 4096 } text && !text.Contains('\0') ? text : throw InvalidPhysicalDeathJson();

    private static ushort ReadPhysicalDeathUShort(JsonElement value) => value.ValueKind == JsonValueKind.Number &&
        value.TryGetUInt16(out var number) ? number : throw InvalidPhysicalDeathJson();

    private static bool ReadPhysicalDeathBoolean(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true, JsonValueKind.False => false, _ => throw InvalidPhysicalDeathJson(),
    };

    private static JsonException InvalidPhysicalDeathJson() => new("Invalid physical death22 field or bound.");
}
