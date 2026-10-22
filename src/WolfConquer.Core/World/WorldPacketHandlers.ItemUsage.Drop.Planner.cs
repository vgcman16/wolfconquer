using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyItemDropPlan(WorldSession Updated, PlayerItemSnapshotItem Item,
        bool GroundDrop, byte[] RemovalFrame);

    // MsgItemUsuagePacket.DropItem1336..1378: PIN precedes this caller-owned
    // inventory/Anima/protected-ID/lock sequence; REMOVE always uses Removefull.
    private static bool TryPlanLegacyItemDrop(WorldPacketHandlerContext context, WorldSession current,
        MsgItemUsagePacket packet, out LegacyItemDropPlan plan, out string reason, out byte[]? rejection)
    {
        plan = null!;
        rejection = null;
        reason = string.Empty;
        if (current.LegacyTradeActive || context.Trades?.Contains(context.Connection) == true ||
            !LegacyTradeEscrowClaims.TryRead(current.Character.Attributes, out var claim, out _) || claim is not null)
        { reason = "active trade or unresolved escrow"; return false; }
        var matches = current.PlayerItems.InventoryItems.Where(item => item.Uid == packet.Id).ToArray();
        if (packet.Id == 0 || matches.Length != 1)
        { reason = "inventory item was not found or item UID is ambiguous"; return false; }
        var item = matches[0];
        if (IsLegacySecurityAnimaProtectedItem(item) && LegacySecurityAnimaItemOperationNeedsPin(current))
        {
            reason = "anima-pin-not-activated";
            rejection = LegacyNpcReplyPacket.ForMessageBox(LegacySecurityAnimaActivationRequiredMessage).ToFrameBytes();
            return false;
        }
        // Its guarded native MessageBox remains held; retaining the item does
        // not confer any missing participation/callback/timer owner.
        if (LegacyNativeItemRestrictions.IsDropProtectedItem(item.ItemId))
        { reason = "nativeProtectedItemDrop"; return false; }
        if (item.Locked > 0 || item.Inscribed > 0)
        { reason = "locked or inscribed items cannot be dropped"; return false; }
        var updated = current with { PlayerItems = RemoveInventoryItem(current.PlayerItems, item) };
        plan = new(updated, item,
            item.Bound < 1 && !LegacyNativeItemRestrictions.IsUnableTradeItem(item.ItemId),
            new MsgItemUsagePacket(item.Uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes());
        return true;
    }

    private static bool TryReserveLegacyItemDrop(WorldPacketHandlerContext context, WorldSession current,
        LegacyItemDropPlan plan, out FloorItemRuntimeStateStore.GroundReservation? reservation, out string reason)
    {
        reservation = null;
        reason = "floorStoreUnavailable";
        if (context.FloorItems is not { } floorItems) return false;
        if (!LegacyFloorItemVisibility.TryReadDynamicId(current.Character, out var dynamicId))
        { reason = "invalidDynamicId"; return false; }
        var packet = LegacyFloorItemPacket.FromDroppedItem(plan.Item, current.Character, NextLegacyFloorItemUid());
        var entry = FloorItemRuntimeEntry.ForItem(current.Character.MapId, packet, plan.Item, dynamicId: dynamicId);
        if (floorItems.TryReserveGround(entry, DateTimeOffset.UtcNow, 0,
            (map, x, y) => TryValidateStaticTerrainLocation(context, map, x, y, out _), out reservation))
        { reason = string.Empty; return true; }
        reason = "no valid ground tile was available or floor UID is reserved";
        return false;
    }
}
