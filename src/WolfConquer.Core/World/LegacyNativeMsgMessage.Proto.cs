#nullable disable

using ProtoBuf;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgMessage
{
    // Complete native MsgTalkProto attribute contract. This uses the genuine
    // pinned serializer through Packet, not the manual combat-gear projection.
    [ProtoContract]
    public class MsgTalkProto
    {
        [ProtoMember(1, IsRequired = true)]
        public uint Color;

        [ProtoMember(2, IsRequired = true)]
        public uint ChatType;

        [ProtoMember(3, IsRequired = true)]
        public uint MessageUID1;

        [ProtoMember(4, IsRequired = true)]
        public uint MessageUID2;

        [ProtoMember(5, IsRequired = true)]
        public uint ServerID;

        [ProtoMember(6, IsRequired = true)]
        public uint Mesh;

        [ProtoMember(7, IsRequired = true)]
        public uint UnionType;

        [ProtoMember(8, IsRequired = true)]
        public uint Count;

        [ProtoMember(9, IsRequired = true)]
        public uint New1;

        [ProtoMember(10, IsRequired = true)]
        public uint New2;

        [ProtoMember(14, IsRequired = true)]
        public string[] Content;
    }
}
