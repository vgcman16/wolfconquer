using ProtoBuf;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgCollectionStorage
{
    [ProtoContract]
    public class Scrap
    {
        [ProtoMember(1, IsRequired = true)]
        public uint Type = 0u;

        [ProtoMember(2, IsRequired = true)]
        public uint Amount = 0u;
    }

    [ProtoContract]
    public class item
    {
        [ProtoMember(1, IsRequired = true)]
        public uint Type = 0u;

        [ProtoMember(2, IsRequired = true)]
        public uint Activate = 0u;

        [ProtoMember(3, IsRequired = true)]
        public uint Amount = 0u;

        [ProtoMember(4, IsRequired = true)]
        public uint RemainingTime = 0u;

        [ProtoMember(5, IsRequired = true)]
        public uint F5 = 0u;
    }
}
