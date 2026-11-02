using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRelicDecomposeJournalOperation ReadRelicDecompose(JsonElement element)
    {
        var row = GetProperties(element, "Request", "Allocation", "OriginalFrame", "Selections", "RewardDurability");
        var entries = row["Selections"];
        if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() is < 1 or > 49)
            throw new JsonException("Invalid relic decomposition selection count.");
        var selected = new List<LegacyRelicDecomposeSelection>();
        foreach (var entry in entries.EnumerateArray())
        {
            var fields = GetProperties(entry, "Uid", "ItemId", "Bound");
            selected.Add(new(ReadRuneDecomposeUInt(fields["Uid"]), ReadRuneDecomposeUInt(fields["ItemId"]),
                checked((byte)ReadRuneDecomposeUInt(fields["Bound"]))));
        }
        try
        {
            return new(ReadRuneDecomposeRequest(row["Request"]), ReadRuneDecomposeAllocation(row["Allocation"]),
                ReadBytes(row["OriginalFrame"], allowNull: false)!, selected.ToArray(),
                checked((ushort)ReadRuneDecomposeUInt(row["RewardDurability"])));
        }
        catch (Exception error) when (error is InvalidDataException or OverflowException or ArgumentException)
        { throw new JsonException("Invalid relic decomposition recipe.", error); }
    }
}
