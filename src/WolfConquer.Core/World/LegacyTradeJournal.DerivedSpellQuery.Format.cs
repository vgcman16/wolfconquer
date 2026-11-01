using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyDerivedSpellQueryOperation ReadDerivedSpellQuery(JsonElement element)
    {
        var fields = GetProperties(element, "TargetUid", "Source", "Alternate", "ItemsBefore", "RankingRows", "GrantedRow");
        if (fields["TargetUid"].ValueKind != JsonValueKind.Number || !fields["TargetUid"].TryGetUInt32(out var uid) ||
            fields["Source"].ValueKind != JsonValueKind.Number || !fields["Source"].TryGetInt32(out var source) ||
            source is not (1 or 2)) throw InvalidDerivedSpellQueryJson();
        var ranks = ReadPhysicalDeathArray(fields["RankingRows"], ushort.MaxValue).Select(row =>
        {
            var values = GetProperties(row, "CharacterId", "Fields");
            if (values["CharacterId"].ValueKind != JsonValueKind.Number || !values["CharacterId"].TryGetInt32(out var id))
                throw InvalidDerivedSpellQueryJson();
            return new LegacyDerivedSpellQueryRankInput(id, ReadPhysicalDeathFields(values["Fields"]));
        }).ToArray();
        MagicType? granted = null;
        if (fields["GrantedRow"].ValueKind != JsonValueKind.Null)
        {
            var values = GetProperties(fields["GrantedRow"], "Id", "TypeId", "Name", "Fields");
            if (values["Id"].ValueKind != JsonValueKind.Number || values["TypeId"].ValueKind != JsonValueKind.Number ||
                !values["Id"].TryGetInt32(out var id) || !values["TypeId"].TryGetInt32(out var type)) throw InvalidDerivedSpellQueryJson();
            granted = new MagicType(id, type, ReadPhysicalDeathString(values["Name"]), ReadPhysicalDeathFields(values["Fields"]));
        }
        return new(uid, (LegacyDerivedSpellQuerySource)source, ReadPhysicalDeathBoolean(fields["Alternate"]),
            ReadBytes(fields["ItemsBefore"], allowNull: false)!, ranks, granted);
    }

    private static JsonException InvalidDerivedSpellQueryJson() => new("Invalid derived spell query23 field or bound.");
}
