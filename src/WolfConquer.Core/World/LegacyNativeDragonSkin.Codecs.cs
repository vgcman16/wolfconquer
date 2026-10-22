using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeDragonSkin
{
    public static LegacyNativePacket CreateAnimaSkin(this LegacyNativePacket stream, MsgDragonSkin obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2415);
        return stream;
    }

    public static void GetDragonSkin(this LegacyNativePacket stream, out MsgDragonSkin pQuery)
    {
        pQuery = new MsgDragonSkin();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }
}
