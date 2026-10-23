using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeItemViewPacket
{
    // Whole native MsgBuilder2095..2146. RemainingTime is intentionally read
    // twice, not cached; source's conditional four-byte Activate field remains.
    public static LegacyNativePacket ItemViewCreate(this LegacyNativePacket stream, uint PlayerUID,
        long Cost, LegacyNativeGameItemRuntime DateItem, LegacyNativeMsgItemView.ActionMode Mode)
    {
        stream.InitWriter();
        stream.Write(DateItem.Uid);
        stream.Write(PlayerUID);
        stream.Write(Cost);
        stream.Write(DateItem.ITEM_ID);
        stream.Write(DateItem.Durability);
        stream.Write(DateItem.MaximDurability);
        stream.Write((ushort)Mode);
        stream.Write((byte)DateItem.Position);
        stream.Write(DateItem.SocketProgress);
        stream.Write((byte)DateItem.SocketOne);
        stream.Write((byte)DateItem.SocketTwo);
        stream.Write(DateItem.SpellID);
        stream.Write((byte)DateItem.Effect);
        stream.Write(DateItem.Plus);
        stream.Write(DateItem.Bless);
        stream.Write(DateItem.Bound);
        stream.Write(DateItem.Enchant);
        stream.Write(DateItem.ProgresGreen);
        stream.Write(DateItem.Suspicious);
        stream.Write((byte)0);
        stream.Write(DateItem.Locked);
        stream.Write((byte)0);
        stream.Write(DateItem.PlusProgress);
        if (DateItem.RemainingTime == 0)
        {
            stream.Write(DateItem.Activate);
        }
        stream.Write(DateItem.RemainingTime);
        stream.Write(DateItem.StackSize);
        stream.Write(DateItem.Purification.PurificationItemID);
        stream.Write(DateItem.Purification.PurificationItemID);
        stream.Write(DateItem.PerfectionLevel);
        stream.Write(DateItem.PerfectionProgress);
        stream.Write(DateItem.OwnerUID);
        stream.Write(DateItem.OwnerName, 32);
        stream.Write(DateItem.Signature, 64);
        stream.Write(DateItem.RuneEXP);
        for (int i = 0; i < DateItem.RelicAttributes.Length; i++)
        {
            stream.Write(DateItem.RelicAttributes[i]);
        }
        stream.Write(DateItem.AnimaItemID);
        stream.Write(0);
        stream.Write(DateItem.MythSoul);
        stream.Write(DateItem.MythSoulPoints);
        stream.Finalize(2275);
        return stream;
    }
}
