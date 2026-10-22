using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgBuilder2790..2821 string-packet primitive contract.
internal static class LegacyNativeStringPacket
{
    public static void GetStringPacket(this LegacyNativePacket stream, out LegacyNativeMsgStringPacket str)
    {
        str = new LegacyNativeMsgStringPacket();
        str.UID = stream.ReadUInt32();
        str.ID = (LegacyNativeMsgStringPacket.StringID)stream.ReadUInt8();
        str.Strings = stream.ReadStringList();
    }

    public static LegacyNativePacket StringPacketCreate(this LegacyNativePacket stream, LegacyNativeMsgStringPacket pac)
    {
        stream.InitWriter();
        if (pac.ID == LegacyNativeMsgStringPacket.StringID.LocationEffect)
        {
            stream.Write(pac.X);
            stream.Write(pac.Y);
        }
        else
        {
            stream.Write(pac.UID);
        }
        stream.Write((byte)pac.ID);
        if (pac.Strings != null)
        {
            stream.Write(pac.Strings);
        }
        else
        {
            stream.ZeroFill(3);
        }
        stream.Finalize(2036);
        return stream;
    }
}
