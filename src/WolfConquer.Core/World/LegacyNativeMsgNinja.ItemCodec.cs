#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    public const ushort MsgNinjaItem_ID = 2393;

    public static LegacyNativePacket CreateNinjaItem(this LegacyNativePacket stream, MsgNinjaItem obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2393);
        return stream;
    }

    public static void GetNinjaItem(this LegacyNativePacket stream, out MsgNinjaItem pQuery)
    {
        pQuery = new MsgNinjaItem();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }
}
