using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static void AppendLegacyFloorPickupItemFrames(List<byte[]> frames,
        LegacyFloorPickupInventoryChange inventory)
    {
        // Native Send emits ItemCreate before SendItemLocked can clear Locked.
        // Retain that first image even though the complete final state is saved
        // before ANY reply. Native extra-UID lifetime ownership remains separate.
        frames.Add(CreateLegacyFloorPickupItemFrame(inventory.InitialSendItem,
            inventory.IsMerge ? LegacyGameItemPacket.UpdateItemMode : LegacyGameItemPacket.AddItemMode,
            inventory.Now));
        if (inventory.InitialSendItem.Locked == 2 && inventory.Item.Locked == 0)
            frames.Add(CreateLegacyFloorPickupItemFrame(inventory.Item,
                LegacyGameItemPacket.UpdateItemMode, inventory.Now));
        else if (inventory.UnlockDate is { } unlockDate)
            frames.Add(unlockDate.ToFrameBytes());
    }

    private static byte[] CreateLegacyFloorPickupItemFrame(PlayerItemSnapshotItem item, ushort mode, DateTime now)
    {
        var frame = LegacyGameItemPacket.FromSnapshotItem(item, mode: mode);
        // Capture getters during preflight, not after committing assets. A bad
        // expiry representation cannot therefore fail after floor consumption.
        if (item.Activate == 1)
            frame = frame with { RemainingTime = unchecked((uint)
                (DateTime.FromBinary(item.Expiration) - now).TotalSeconds) };
        return frame.ToFrameBytes();
    }
}
