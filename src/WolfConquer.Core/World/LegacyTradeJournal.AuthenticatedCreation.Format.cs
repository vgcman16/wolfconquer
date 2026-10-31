using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyAuthenticatedOrdinaryCreationOperation(byte[] Payload, ulong PreparedGeneration,
    LegacyItemUidReservationReceipt Allocation);

internal static partial class LegacyTradeJournal
{
    private static LegacyAuthenticatedOrdinaryCreationOperation ReadAuthenticatedCreation(JsonElement element)
    {
        var fields = GetProperties(element, "Payload", "PreparedGeneration", "Allocation");
        var payload = ReadBytes(fields["Payload"], allowNull: false)!;
        if (payload.Length is < 1 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes ||
            fields["PreparedGeneration"].ValueKind != JsonValueKind.Number ||
            !fields["PreparedGeneration"].TryGetUInt64(out var prepared) || prepared == 0)
            throw new JsonException("Invalid authenticated creation payload or preparation generation.");
        // The allocation has the same exact nine canonical fields emitted by
        // the real registry; reading it grants no producer or UID capability.
        var allocation = ReadRuneDecomposeAllocation(fields["Allocation"]);
        if (allocation.Generation <= prepared || allocation.Count is < 1 or > 4)
            throw new JsonException("Authenticated creation allocation precedes its intent or has an invalid count.");
        return new(payload, prepared, allocation);
    }

    private static string ReadCreationMailbox(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String || element.GetString() is not { Length: > 0 } path ||
            path.Length > 32768 || Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) != path)
            throw new JsonException("Authenticated creation mailbox is not canonical.");
        return path;
    }
}
