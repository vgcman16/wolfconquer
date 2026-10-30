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
        ArgumentNullException.ThrowIfNull(stream);
        if (File_Size < 0 || File_Size > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(File_Size));
        if (count < 0 || count > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(count));
        stream.PrepareWriteAt(4, prepared =>
        {
            prepared.Write(NpcID); prepared.Write(0u); prepared.Write((uint)Action);
            prepared.Write((ushort)File_Size); prepared.Write(ItemUID); prepared.Write((ushort)count);
        });
        return stream;
    }

    public static LegacyNativePacket AddItemWarehouse(this LegacyNativePacket stream, LegacyNativeGameItemRuntime item)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var image = new LegacyNativeItemPacketSnapshot(item);
        return stream.AddCapturedWarehouse(image);
    }

    internal static LegacyNativePacket AddCapturedWarehouse(this LegacyNativePacket stream, LegacyNativeItemPacketSnapshot image)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!image.IsCurrent()) throw new InvalidOperationException("Warehouse item changed before row preparation.");
        stream.PrepareAppend(prepared =>
        {
            prepared.Write(image.Uid); prepared.Write(image.ITEM_ID); prepared.Write((byte)0);
            prepared.Write((byte)image.SocketOne); prepared.Write((byte)image.SocketTwo); prepared.Write(image.SpellID);
            prepared.Write((ushort)image.Effect); prepared.Write(image.Plus); prepared.Write(image.Bless);
            prepared.Write(image.Bound); prepared.Write(image.Enchant); prepared.Write((byte)image.ProgresGreen);
            prepared.Write((uint)image.Effect); prepared.Write(image.Locked); prepared.Write((byte)0);
            prepared.Write(image.SocketProgress); prepared.Write(image.PlusProgress); prepared.Write(image.TimeLeftInMinutes * 60);
            prepared.Write(0u); prepared.Write(image.StackSize); prepared.Write(image.Purification.PurificationItemID);
            prepared.Write((ushort)0); prepared.Write(image.PerfectionLevel); prepared.Write(image.PerfectionProgress);
            prepared.Write(image.OwnerUID); prepared.Write(image.OwnerName, 32); prepared.Write(image.Signature, 60);
            prepared.Write(image.RuneEXP);
            for (int i=0;i<image.RelicCount;i++) prepared.Write(image.RelicWord(i));
            prepared.Write(image.AnimaItemID); prepared.ZeroFill(4); prepared.Write(image.MythSoul);
            prepared.Write(image.MythSoulPoints); prepared.ZeroFill(11); prepared.Write((byte)0);
            prepared.Write(0u); prepared.Write(0u); prepared.Write(0u); prepared.Write(0u);
        });
        return stream;
    }

    public static LegacyNativePacket FinalizeWarehouse(this LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Finalize(2080); return stream;
    }
}
