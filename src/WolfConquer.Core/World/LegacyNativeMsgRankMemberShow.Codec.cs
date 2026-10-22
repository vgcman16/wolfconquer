using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgRankMemberShow
{
    public static LegacyNativePacket CreateMsgRankMemberShow(this LegacyNativePacket stream,
        MsgRankMemberShowProto obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2054);
        return stream;
    }

    public static void GetRankMemberShow(this LegacyNativePacket stream, out MsgRankMemberShowProto pQuery)
    {
        pQuery = new MsgRankMemberShowProto();
        pQuery = stream.ProtoBufferDeserialize(pQuery);
    }
}
