#nullable disable
using ProtoBuf;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgTitleStorage
{
    [ProtoContract]
    public class TitleStorage
    {
        [ProtoMember(1, IsRequired = true)]
        public Action ActionID;

        [ProtoMember(2, IsRequired = true)]
        public uint dwparam1;

        [ProtoMember(3, IsRequired = true)]
        public uint dwparam2;

        [ProtoMember(4, IsRequired = true)]
        public uint dwparam3;

        [ProtoMember(5, IsRequired = true)]
        public Title Title;
    }

    [ProtoContract]
    public class Title
    {
        [ProtoMember(1, IsRequired = true)]
        public uint ID;

        [ProtoMember(2, IsRequired = true)]
        public uint SubId;

        [ProtoMember(3, IsRequired = true)]
        public uint dwparam1;

        [ProtoMember(4, IsRequired = true)]
        public uint dwparam2;
    }
}
