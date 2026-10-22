using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgHundredWeaponsOpt
{
    public static LegacyNativePacket CreateHundredWeaponsOpt(this LegacyNativePacket stream,
        MsgHundredWeaponsOptProto proto)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(proto);
        stream.Finalize(2353);
        return stream;
    }
}
