using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyNpcSaleBlockedInventoryUid = 2_100_075;
    private const string LegacyNpcSaleLockedMessage = "This item is inscribed by your guild or locked.";

    // Native SellItem order: no-sell ITEM_ID, Locked/Inscribed, then the
    // separate selected inventory UID == 2100075 check. No mutation or socket IO.
    private static bool TryRejectLegacyNpcSaleItem(PlayerItemSnapshotItem item, uint selectedUid,
        out string reason, out byte[]? rejection)
    {
        reason = string.Empty;
        rejection = null;
        if (IsLegacyNoSellItem(item.ItemId))
        {
            // Native Player.MessageBox has physical owner/login/life/qualifier/
            // watcher/map and callback/timer semantics not owned by this planner.
            // Retain silent denial rather than fabricate that missing popup owner.
            reason = "item is blocked by the legacy no-sell list";
            return true;
        }
        if (item.Locked > 0 || item.Inscribed > 0)
            reason = "locked or inscribed items cannot be sold";
        else if (selectedUid == LegacyNpcSaleBlockedInventoryUid)
            reason = "inventory UID is blocked by the native no-sell check";
        else return false;

        // GameClient.SendSysMesage defaults to connection-only red TopLeftSystem2005.
        // The existing planner rejection queue sends this only after the lease ends.
        rejection = LegacyMsgMessagePacket.SystemTopLeft(LegacyNpcSaleLockedMessage).ToFrameBytes();
        return true;
    }
}
