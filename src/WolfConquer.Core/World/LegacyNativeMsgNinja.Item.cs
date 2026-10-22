#nullable disable
using ProtoBuf;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    // Complete native physical contract; its private receiver body is in a separate partial.
    [ProtoContract]
    public partial class MsgNinjaItem
    {
        [Flags]
        public enum TypeID
        {
            Load = 0,
            View = 1,
            Add = 2,
            Update = 3
        }

        [ProtoContract]
        public class Item
        {
            [ProtoMember(1, IsRequired = true)]
            public uint ID = 0u;

            [ProtoMember(2, IsRequired = true)]
            public uint Level = 0u;

            [ProtoMember(3, IsRequired = true)]
            public uint Position = 0u;

            [ProtoMember(4, IsRequired = true)]
            public uint Points = 0u;

            [ProtoMember(5, IsRequired = true)]
            public uint UK = 0u;
        }

        [ProtoMember(1, IsRequired = true)]
        public TypeID Type = TypeID.Load;

        [ProtoMember(2, IsRequired = true)]
        public uint UID = 0u;

        [ProtoMember(3, IsRequired = true)]
        public Item[] Items;
    }
}
