using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeItemViewPacket
{
    // Preserve two real clock reads and the native conditional Activate word.
    public static LegacyNativePacket ItemViewCreate(this LegacyNativePacket stream, uint PlayerUID,
        long Cost, LegacyNativeGameItemRuntime DateItem, LegacyNativeMsgItemView.ActionMode Mode)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var image = new LegacyNativeItemPacketSnapshot(DateItem);
        uint remainingProbe = DateItem.RemainingTime;
        uint remainingWire = DateItem.RemainingTime;
        if (!image.IsCurrent()) throw new InvalidOperationException("Item changed before view preparation.");
        stream.PrepareFrame(2275, prepared =>
        {
            prepared.Write(image.Uid);
            prepared.Write(PlayerUID);
            prepared.Write(Cost);
            prepared.Write(image.ITEM_ID);
            prepared.Write(image.Durability);
            prepared.Write(image.MaximDurability);
            prepared.Write((ushort)Mode);
            prepared.Write((byte)image.Position);
            prepared.Write(image.SocketProgress);
            prepared.Write((byte)image.SocketOne);
            prepared.Write((byte)image.SocketTwo);
            prepared.Write(image.SpellID);
            prepared.Write((byte)image.Effect);
            prepared.Write(image.Plus);
            prepared.Write(image.Bless);
            prepared.Write(image.Bound);
            prepared.Write(image.Enchant);
            prepared.Write(image.ProgresGreen);
            prepared.Write(image.Suspicious);
            prepared.Write((byte)0);
            prepared.Write(image.Locked);
            prepared.Write((byte)0);
            prepared.Write(image.PlusProgress);
            if (remainingProbe == 0)
            {
                prepared.Write(image.Activate);
            }
            prepared.Write(remainingWire);
            prepared.Write(image.StackSize);
            prepared.Write(image.Purification.PurificationItemID);
            prepared.Write(image.Purification.PurificationItemID);
            prepared.Write(image.PerfectionLevel);
            prepared.Write(image.PerfectionProgress);
            prepared.Write(image.OwnerUID);
            prepared.Write(image.OwnerName, 32);
            prepared.Write(image.Signature, 64);
            prepared.Write(image.RuneEXP);
            for (int i = 0; i < image.RelicCount; i++)
            {
                prepared.Write(image.RelicWord(i));
            }
            prepared.Write(image.AnimaItemID);
            prepared.Write(0);
            prepared.Write(image.MythSoul);
            prepared.Write(image.MythSoulPoints);
        });
        return stream;
    }
}
