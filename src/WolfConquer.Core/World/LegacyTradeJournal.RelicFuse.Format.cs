using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Strict DATA decoding only. Startup must still bind the decoded request
    // to the actual registry row before applying any retained journal image.
    private static LegacyRelicFuseJournalOperation ReadRelicFuse(JsonElement element)
    {
        var fields = GetProperties(element, "Request", "Allocation", "FirstUid", "SecondUid",
            "ResultItemId", "Bound", "Durability", "Words");
        var raw = fields["Words"];
        if (raw.ValueKind != JsonValueKind.Array || raw.GetArrayLength() != 5)
            throw new JsonException("Invalid relic fuse word count.");
        var words = raw.EnumerateArray().Select(ReadRuneDecomposeUInt).ToArray();
        try
        {
            return new LegacyRelicFuseJournalOperation(ReadRuneDecomposeRequest(fields["Request"]),
                ReadRuneDecomposeAllocation(fields["Allocation"]), ReadRuneDecomposeUInt(fields["FirstUid"]),
                ReadRuneDecomposeUInt(fields["SecondUid"]), ReadRuneDecomposeUInt(fields["ResultItemId"]),
                checked((byte)ReadRuneDecomposeUInt(fields["Bound"])),
                checked((ushort)ReadRuneDecomposeUInt(fields["Durability"])), words);
        }
        catch (Exception error) when (error is InvalidDataException or OverflowException)
        { throw new JsonException("Invalid relic fuse recipe.", error); }
    }
}
