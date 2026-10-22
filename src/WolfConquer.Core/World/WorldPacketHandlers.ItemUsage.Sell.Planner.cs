using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyNpcSalePlan(WorldSession Updated, uint ItemUid, long Value,
        bool UpdatesMoney, long WarehouseOverflow, byte[] InventoryFrame);

    // MsgItemUsuagePacket.SellItem, including the Money setter's inventory cap.
    // File/schema/underflow failures are deliberately fenced by the managed adapter.
    private static bool TryPlanLegacyNpcSale(WorldPacketHandlerContext context, WorldSession session,
        MsgItemUsagePacket packet, out LegacyNpcSalePlan plan, out string reason, out byte[]? rejection)
    {
        plan = null!;
        reason = string.Empty;
        rejection = null;
        if (packet.DwParam is 0 or > uint.MaxValue)
        { reason = "item UID is outside legacy range"; return false; }
        if (session.LegacyTradeActive || context.Trades?.Contains(context.Connection) == true ||
            !LegacyTradeEscrowClaims.TryRead(session.Character.Attributes, out var claim, out _) || claim is not null)
        { reason = "active trade or unresolved escrow"; return false; }
        if (!CanSeeSellNpc(context, session.Character, packet.Id))
        { reason = "visible NPC was not found"; return false; }
        var uid = (uint)packet.DwParam;
        var inventory = session.PlayerItems.InventoryItems.Where(item => item.Uid == uid).ToArray();
        if (inventory.Length != 1 || CountLegacyNpcSaleUid(session.PlayerItems, uid) != 1)
        { reason = "inventory item was not found or item UID is ambiguous"; return false; }
        var item = inventory[0];
        if (IsLegacySecurityAnimaProtectedItem(item) && LegacySecurityAnimaItemOperationNeedsPin(session))
        {
            reason = "anima-pin-not-activated";
            rejection = LegacyNpcReplyPacket.ForMessageBox(LegacySecurityAnimaActivationRequiredMessage).ToFrameBytes();
            return false;
        }
        if (TryRejectLegacyNpcSaleItem(item, uid, out reason, out rejection)) return false;
        if (!TryReadGoldWorth(context.WorldData, item.ItemId, out var goldWorth))
        { reason = "itemtype gold worth was not loaded"; return false; }
        var value = CalculateLegacyNpcSaleValue(goldWorth, item.Durability, item.MaximumDurability);
        var updatesMoney = item.Durability > 0 && item.Durability <= item.MaximumDurability;
        PlayerItemSnapshot updatedItems;
        byte[] inventoryFrame;
        // Native Inventory.Update(REMOVE, Removefull:false), before Money's setter.
        if (item.StackSize > 1 && item.Position < 40)
        {
            var remaining = item.WithStackSize((ushort)(item.StackSize - 1));
            updatedItems = ReplaceInventoryItem(session.PlayerItems, item, remaining);
            inventoryFrame = LegacyGameItemPacket.FromSnapshotItem(remaining,
                mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
        }
        else
        {
            updatedItems = RemoveInventoryItem(session.PlayerItems, item);
            inventoryFrame = new MsgItemUsagePacket(uid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes();
        }
        var updated = session with { PlayerItems = updatedItems };
        var warehouse = ReadWarehouseMoney(session.Character);
        if (session.Character.Money < 0 || warehouse > long.MaxValue)
        { reason = "wallet is outside the supported native range"; return false; }
        var money = session.Character.Money;
        var updatedWarehouse = (long)warehouse;
        long overflow = 0;
        if (updatesMoney)
        {
            try
            {
                money = checked(session.Character.Money + value);
                if (money < 0) { reason = "sale would underflow money"; return false; }
                overflow = Math.Max(0, money - LegacyMaxInventoryMoney);
                money -= overflow;
                updatedWarehouse = checked((long)warehouse + overflow);
            }
            catch (OverflowException)
            { reason = "sale would overflow money or warehouse money"; return false; }
        }
        // Keep the live projection coherent with the physical canonical field,
        // including a zero-payout removal from a legacy file without WHMoney.
        var attrs = new Dictionary<string, string>(updated.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = money.ToString(CultureInfo.InvariantCulture),
            ["WHMoney"] = updatedWarehouse.ToString(CultureInfo.InvariantCulture),
        };
        if (attrs.ContainsKey("WarehouseMoney")) attrs["WarehouseMoney"] = attrs["WHMoney"];
        updated = updated with { Character = updated.Character with { Money = money, Attributes = attrs } };
        plan = new(updated, uid, value, updatesMoney, overflow, inventoryFrame);
        return true;
    }

    // Preserve the native Int32 multiplication/truncation order, not a widened
    // price calculation. Valid durability invokes Money even for zero worth.
    private static long CalculateLegacyNpcSaleValue(uint worth, ushort durability, ushort maximum)
    {
        var value = (int)(worth / 3);
        if (durability > 0 && durability < maximum) value = unchecked(value * durability) / maximum;
        return durability > 0 && durability <= maximum ? value : 0;
    }

    private static int CountLegacyNpcSaleUid(PlayerItemSnapshot items, uint uid)
    {
        int Count(PlayerItemSnapshotItem item) => (item.Uid == uid ? 1 : 0) + item.DepositedItems.Sum(Count);
        return items.InventoryItems.Concat(items.EquipmentItems).Concat(items.RuneItems)
            .Concat(items.WardrobeItems).Concat(items.WarehouseItems).Concat(items.RelicFuseItems)
            .Concat(items.MythsoulBagItems).Sum(Count);
    }
}
