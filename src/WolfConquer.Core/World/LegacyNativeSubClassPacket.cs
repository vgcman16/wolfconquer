#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native primitive GetSubClass/SubClassCreate overloads only.
internal static class LegacyNativeSubClassPacket
{
    public static void GetSubClass(this LegacyNativePacket stream,
        out LegacyNativeMsgSubClass.Action mode, out LegacyNativeDbLevExp.Sort clas)
    {
        mode = (LegacyNativeMsgSubClass.Action)stream.ReadUInt16();
        clas = unchecked((LegacyNativeDbLevExp.Sort)stream.ReadInt8());
    }

    public static LegacyNativePacket SubClassCreate(this LegacyNativePacket stream,
        LegacyNativeMsgSubClass.Action mode, LegacyNativeDbLevExp.Sort clas, byte Level, ushort StudyReceive)
    {
        stream.InitWriter();
        stream.Write((ushort)mode);
        stream.Write((byte)clas);
        stream.Write(Level);
        stream.ZeroFill(6);
        stream.Write(StudyReceive);
        stream.ZeroFill(10);
        stream.Finalize(2033);
        return stream;
    }

    public static void GetSubClass(this LegacyNativePacket stream,
        out LegacyNativeMsgSubClass.Action mode, out LegacyNativeMsgSubClass.SubClases[] list)
    {
        mode = (LegacyNativeMsgSubClass.Action)stream.ReadUInt16();
        stream.SeekForward(16);
        int num = stream.ReadInt32();
        list = new LegacyNativeMsgSubClass.SubClases[num];
        for (int i = 0; i < num; i++)
        {
            list[i] = new LegacyNativeMsgSubClass.SubClases();
            list[i].ID = (LegacyNativeDbLevExp.Sort)stream.ReadUInt8();
            list[i].Phrase = stream.ReadUInt8();
            list[i].Level = stream.ReadUInt8();
        }
    }

    public static LegacyNativePacket SubClassCreate(this LegacyNativePacket stream,
        LegacyNativeMsgSubClass.Action mode, ushort study, ushort StudyReceive,
        LegacyNativeMsgSubClass.SubClases[] list = null)
    {
        stream.InitWriter();
        stream.Write((ushort)mode);
        stream.Write(study);
        stream.ZeroFill(6);
        stream.Write(StudyReceive);
        stream.ZeroFill(6);
        if (list != null)
        {
            stream.Write(list.Length);
            for (int i = 0; i < list.Length; i++)
            {
                stream.Write((byte)list[i].ID);
                stream.Write(list[i].Phrase);
                stream.Write(list[i].Level);
            }
        }
        else
        {
            stream.ZeroFill(4);
        }
        stream.Finalize(2033);
        return stream;
    }
}
