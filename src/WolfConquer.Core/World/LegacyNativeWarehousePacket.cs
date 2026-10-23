using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeWarehousePacket
{
    // Whole native MsgBuilder3378..3450. Reader/writer width asymmetry is literal.
    public static void GetWarehouse(this LegacyNativePacket stream, out uint NpcID,
        out LegacyNativeMsgWarehouse.DepositActionID Action, out uint ItemUID)
    {
        NpcID = stream.ReadUInt32();
        uint num = stream.ReadUInt32();
        Action = (LegacyNativeMsgWarehouse.DepositActionID)stream.ReadUInt16();
        uint num2 = stream.ReadUInt32();
        ItemUID = stream.ReadUInt32();
    }

    public static LegacyNativePacket WarehouseCreate(this LegacyNativePacket stream, uint NpcID,
        LegacyNativeMsgWarehouse.DepositActionID Action, uint ItemUID, int File_Size, int count)
    {
        stream.InitWriter();
        stream.Write(NpcID); stream.Write(0u); stream.Write((uint)Action);
        stream.Write((ushort)File_Size); stream.Write(ItemUID); stream.Write((ushort)count);
        return stream;
    }

    public static LegacyNativePacket AddItemWarehouse(this LegacyNativePacket stream, LegacyNativeGameItemRuntime item)
    {
        stream.Write(item.Uid); stream.Write(item.ITEM_ID); stream.Write((byte)0);
        stream.Write((byte)item.SocketOne); stream.Write((byte)item.SocketTwo); stream.Write(item.SpellID);
        stream.Write((ushort)item.Effect); stream.Write(item.Plus); stream.Write(item.Bless);
        stream.Write(item.Bound); stream.Write(item.Enchant); stream.Write((byte)item.ProgresGreen);
        stream.Write((uint)item.Effect); stream.Write(item.Locked); stream.Write((byte)0);
        stream.Write(item.SocketProgress); stream.Write(item.PlusProgress); stream.Write(item.TimeLeftInMinutes * 60);
        stream.Write(0u); stream.Write(item.StackSize); stream.Write(item.Purification.PurificationItemID);
        stream.Write((ushort)0); stream.Write(item.PerfectionLevel); stream.Write(item.PerfectionProgress);
        stream.Write(item.OwnerUID); stream.Write(item.OwnerName, 32); stream.Write(item.Signature, 60);
        stream.Write(item.RuneEXP);
        for (int i=0;i<item.RelicAttributes.Length;i++) stream.Write(item.RelicAttributes[i]);
        stream.Write(item.AnimaItemID); stream.ZeroFill(4); stream.Write(item.MythSoul);
        stream.Write(item.MythSoulPoints); stream.ZeroFill(11); stream.Write((byte)0);
        stream.Write(0u); stream.Write(0u); stream.Write(0u); stream.Write(0u);
        return stream;
    }

    public static LegacyNativePacket FinalizeWarehouse(this LegacyNativePacket stream)
    {
        stream.Finalize(2080); return stream;
    }
}
