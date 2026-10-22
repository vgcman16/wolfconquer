using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    public const ushort MsgNinjaInfo_ID = 2394;

    public static LegacyNativePacket CreateNinjaInfo(this LegacyNativePacket stream, MsgNinjaInfo obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2394);
        return stream;
    }

    public static void GetNinjaInfo(this LegacyNativePacket stream, out MsgNinjaInfo pQuery)
    {
        pQuery = new MsgNinjaInfo();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }
}
