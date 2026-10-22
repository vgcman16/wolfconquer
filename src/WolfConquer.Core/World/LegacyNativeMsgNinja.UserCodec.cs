using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    public const ushort MsgNinjaUser_ID = 2395;

    public static LegacyNativePacket CreateNinjaUser(this LegacyNativePacket stream, MsgNinjaUser obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2395);
        return stream;
    }
}
