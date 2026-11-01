using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyPairMovementOperation ReadPairMovement(JsonElement element)
    {
        var fields = GetProperties(element, "Kind", "ActorAccountId", "ActorUid", "PartnerAccountId", "PartnerUid",
            "MapId", "DynamicId", "FromX", "FromY", "ToX", "ToY", "DirectionOrFacing");
        return new((LegacyPairMovementKind)ReadPairMovementUInt(fields["Kind"]),
            ReadPairMovementAccount(fields["ActorAccountId"]), ReadPairMovementUInt(fields["ActorUid"]),
            ReadPairMovementAccount(fields["PartnerAccountId"]), ReadPairMovementUInt(fields["PartnerUid"]),
            ReadPairMovementUInt(fields["MapId"]), ReadPairMovementUInt(fields["DynamicId"]),
            checked((ushort)ReadPairMovementUInt(fields["FromX"])), checked((ushort)ReadPairMovementUInt(fields["FromY"])),
            checked((ushort)ReadPairMovementUInt(fields["ToX"])), checked((ushort)ReadPairMovementUInt(fields["ToY"])),
            checked((ushort)ReadPairMovementUInt(fields["DirectionOrFacing"])));
    }

    private static uint ReadPairMovementUInt(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetUInt32(out var value) ? value :
            throw new JsonException("Pair movement field is not UInt32.");

    private static long ReadPairMovementAccount(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value) && value > 0 ? value :
            throw new JsonException("Pair movement account is not a positive Int64.");
}
