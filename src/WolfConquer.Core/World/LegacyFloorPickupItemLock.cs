using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// MsgGameItem.SendItemLocked298..321 after the initial ItemCreate. Pickup
// admission has already proved the physical client is on its own server.
internal static class LegacyFloorPickupItemLock
{
    internal static bool TryProject(PlayerItemSnapshotItem item, DateTime now,
        out PlayerItemSnapshotItem finalItem, out LegacyItemLockPacket? unlockDate, out string reason)
    {
        finalItem = null!;
        unlockDate = null;
        reason = "floorPickupItemLockInputInvalid";
        if (item is null) return false;
        if (item.Locked != 2)
        {
            finalItem = item;
            reason = string.Empty;
            return true;
        }

        var expired = item.UnlockTimer == 0;
        if (!expired)
        {
            try
            {
                // Native Core.GetTimer uses YYYYMMDD at midnight and the
                // comparison is strictly greater, not greater-or-equal.
                var year = item.UnlockTimer / 10000;
                var month = item.UnlockTimer / 100 - year * 100;
                var day = item.UnlockTimer - year * 10000 - month * 100;
                expired = now > new DateTime(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            { reason = "floorPickupItemLockTimerInvalid"; return false; }
        }

        finalItem = expired ? item.WithLocked(0) : item;
        if (!expired)
            unlockDate = new(item.Uid, LegacyItemLockPacket.UnlockDateType,
                LegacyItemLockPacket.UnlockDateLockedMode, unchecked((uint)item.UnlockTimer));
        reason = string.Empty;
        return true;
    }
}
