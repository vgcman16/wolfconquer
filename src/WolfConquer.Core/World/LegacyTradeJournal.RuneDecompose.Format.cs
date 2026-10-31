using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRuneDecomposeOperation ReadRuneDecompose(JsonElement element)
    {
        var fields = GetProperties(element, "SelectedUid", "Progress", "RuneMetadata", "RewardMetadata", "Request", "Allocation", "FragmentColor");
        var row = GetProperties(fields["Progress"], "Family", "Level", "Progress");
        var progress = new LegacyRuneDecomposeProgressRow(ReadRuneDecomposeUInt(row["Family"]),
            checked((byte)ReadRuneDecomposeUInt(row["Level"])), ReadRuneDecomposeUInt(row["Progress"]));
        var array = fields["RuneMetadata"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > ushort.MaxValue)
            throw new JsonException("Invalid Rune decomposition provider metadata count.");
        var runes = new List<LegacyRuneMetadata>();
        foreach (var entry in array.EnumerateArray())
        {
            row = GetProperties(entry, "ItemId", "RuneSpellId", "RequiredClass", "RequiredLevel", "IsRuneSkill");
            var classification = row["IsRuneSkill"];
            if (classification.ValueKind is not (JsonValueKind.Null or JsonValueKind.True or JsonValueKind.False))
                throw new JsonException("Invalid Rune decomposition skill classification.");
            runes.Add(new(ReadRuneDecomposeUInt(row["ItemId"]), ReadRuneDecomposeUInt(row["RuneSpellId"]),
                ReadRuneDecomposeUInt(row["RequiredClass"]), ReadRuneDecomposeUInt(row["RequiredLevel"]),
                classification.ValueKind == JsonValueKind.Null ? null : classification.GetBoolean()));
        }
        array = fields["RewardMetadata"];
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 2)
            throw new JsonException("Rune decomposition needs exactly two reward metadata rows.");
        var rewards = new List<LegacyRuneDecomposeRewardMetadata>();
        foreach (var entry in array.EnumerateArray())
        {
            row = GetProperties(entry, "ItemId", "MaximumStackSize", "Durability", "TimeMinutes");
            rewards.Add(new(ReadRuneDecomposeUInt(row["ItemId"]), checked((ushort)ReadRuneDecomposeUInt(row["MaximumStackSize"])),
                row["Durability"].ValueKind == JsonValueKind.Null ? null : checked((ushort)ReadRuneDecomposeUInt(row["Durability"])),
                ReadRuneDecomposeUInt(row["TimeMinutes"])));
        }
        var operation = new LegacyRuneDecomposeOperation(ReadRuneDecomposeUInt(fields["SelectedUid"]), progress,
            runes.ToArray(), rewards.ToArray(), ReadRuneDecomposeRequest(fields["Request"]),
            ReadRuneDecomposeAllocation(fields["Allocation"]), fields["FragmentColor"].ValueKind == JsonValueKind.Null
                ? null : checked((byte)ReadRuneDecomposeUInt(fields["FragmentColor"])));
        operation.Validate(); return operation;
    }

    private static LegacyItemUidReservationRequest ReadRuneDecomposeRequest(JsonElement element)
    {
        var row = GetProperties(element, "OperationId", "Writer", "AccountId", "CharacterUid", "PayloadSha256", "Count");
        return new(ReadRuneDecomposeGuid(row["OperationId"]), ReadRuneDecomposeText(row["Writer"]),
            ReadRuneDecomposeUInt(row["AccountId"]), ReadRuneDecomposeUInt(row["CharacterUid"]),
            ReadRuneDecomposeText(row["PayloadSha256"]), checked((int)ReadRuneDecomposeUInt(row["Count"])));
    }

    private static LegacyItemUidReservationReceipt ReadRuneDecomposeAllocation(JsonElement element)
    {
        var row = GetProperties(element, "DomainId", "OperationId", "Writer", "AccountId", "CharacterUid",
            "PayloadSha256", "Generation", "First", "Count");
        if (row["Generation"].ValueKind != JsonValueKind.Number || !row["Generation"].TryGetUInt64(out var generation))
            throw new JsonException("Invalid Rune decomposition allocation generation.");
        return new(ReadRuneDecomposeGuid(row["DomainId"]), ReadRuneDecomposeGuid(row["OperationId"]),
            ReadRuneDecomposeText(row["Writer"]), ReadRuneDecomposeUInt(row["AccountId"]), ReadRuneDecomposeUInt(row["CharacterUid"]),
            ReadRuneDecomposeText(row["PayloadSha256"]), generation, ReadRuneDecomposeUInt(row["First"]),
            checked((int)ReadRuneDecomposeUInt(row["Count"])));
    }

    private static uint ReadRuneDecomposeUInt(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number)
            ? number : throw new JsonException("Invalid Rune decomposition unsigned input.");

    private static string ReadRuneDecomposeText(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: >= 1 and <= 64 } text)
            throw new JsonException("Invalid Rune decomposition allocation text.");
        return text;
    }

    private static Guid ReadRuneDecomposeGuid(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: 36 } text ||
            !Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || id.ToString("D") != text)
            throw new JsonException("Rune decomposition Guid differs from its actual canonical serialized form.");
        return id;
    }
}
