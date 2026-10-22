using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgTitleStorage
{
    public static LegacyNativePacket CreateTitleStorage(this LegacyNativePacket stream, TitleStorage obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2313);
        return stream;
    }

    public static void GetTitleStorage(this LegacyNativePacket stream, out TitleStorage pQuery)
    {
        pQuery = new TitleStorage();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }
}
