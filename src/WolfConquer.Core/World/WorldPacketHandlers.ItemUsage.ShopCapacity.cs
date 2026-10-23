using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // This admission belongs to normal shop inventory grants only. Returned
    // items, arena awards and other creator callers have different destinations.
    private static bool HasLegacyShopPurchaseCapacity(PlayerItemSnapshot items, int requiredRows)
    {
        if (items is null || items.InventoryItems is null || requiredRows < 1)
            return false;
        var occupied = items.InventoryItems.Count;
        return occupied >= 0 && occupied <= LegacyInventorySlots &&
            requiredRows <= LegacyInventorySlots - occupied;
    }

    private static ValueTask LogLegacyShopCapacityRefusedAsync(
        WorldPacketHandlerContext context, string operation, int requiredRows, PlayerItemSnapshot items) =>
        new(context.Log.WriteLineAsync(
            $"WorldServer skipped MsgItemUsage {operation} {FormatSession(context)} " +
            $"requiredInventoryRows={requiredRows} loadedInventoryItems={items?.InventoryItems?.Count}; " +
            "inventory capacity is insufficient."));
}
