using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgUserTotalRefineLev contract and producer. No receiver/host route.
internal static class LegacyNativeMsgUserTotalRefineLev
{
    [ProtoContract]
    public class TotalRefineLev
    {
        [ProtoMember(1, IsRequired = true)]
        public uint Type;

        [ProtoMember(2, IsRequired = true)]
        public uint UID;

        [ProtoMember(3, IsRequired = true)]
        public uint Level;
    }

    public static LegacyNativePacket UserTotalRefineLevCreate(this LegacyNativePacket stream, TotalRefineLev obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2128);
        return stream;
    }
}
