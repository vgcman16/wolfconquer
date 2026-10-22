using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Only MsgGameItem.Send's state projection before ItemCreate. Sending item
// extras/locks and the physical SQL/MAC/Pipe owners are separate native seams.
internal static class LegacyFloorPickupItemSend
{
    internal static bool TryProject(PlayerItemSnapshotItem item, ItemType catalog,
        out PlayerItemSnapshotItem projected, out string reason)
    {
        projected = null!;
        reason = "floorPickupItemSendBindingInvalid";
        if (item is null || catalog is null || catalog.Id <= 0 || item.ItemId != (uint)catalog.Id)
            return false;

        var maximumDurability = item.MaximumDurability;
        // DBItem.Convert uses UInt16 at field21. This guarded projection does
        // not narrow malformed/widened catalog values into a valid item image.
        if (maximumDurability == 0 && !catalog.TryGetLegacyDurability(out maximumDurability))
        { reason = "floorPickupItemSendCatalogDurabilityInvalid"; return false; }

        var position = LegacyNativeItemPosition.ItemPosition(item.ItemId);
        var plus = position == 0 ? (byte)0 : item.Plus;
        if (item.ItemId is >= 730001 and <= 730008)
            plus = (byte)(item.ItemId % 10);
        projected = maximumDurability == item.MaximumDurability && plus == item.Plus
            ? item : item.WithMaximumDurabilityAndPlus(maximumDurability, plus);
        if (position is 15 or 16)
        {
            // Native assigns Activate=1 and the raw minutes setter UInt32.Max.
            // Its public getter (used by both wire and save) returns zero while
            // active; EndDate is left exactly as selected by Inventory.Add.
            projected = projected.WithActivation(1, projected.Expiration);
        }
        else if (projected.Activate != 0 && projected.TimeLeftInMinutes != 0)
        {
            // MsgGameItem.TimeLeftInMinutes182..195 hides the raw backing
            // minutes for ANY nonzero Activate, not just accessories/timers.
            // Project only this selected pickup output, not old stored items.
            projected = projected.WithActivation(projected.Activate, projected.Expiration);
        }
        reason = string.Empty;
        return true;
    }
}
