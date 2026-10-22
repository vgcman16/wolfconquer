using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native DragonSkin message contract. Process and its packet attribute
// are wholly absent pending the genuine inventory graph; no receiver is stubbed.
internal static partial class LegacyNativeDragonSkin
{
    [ProtoContract]
    public class MsgDragonSkin
    {
        [Flags]
        public enum TypeID
        {
            MyShow = 0,
            PlayerView = 1,
            Unlock = 2,
            Retrieve = 3,
            Select = 4,
            Restore = 5,
            Effect = 6
        }

        [ProtoMember(1)]
        public TypeID Type = TypeID.MyShow;

        [ProtoMember(2)]
        public uint UserID = 0u;

        [ProtoMember(3)]
        public uint SkinType = 0u;

        [ProtoMember(4)]
        public bool OK = false;
    }
}
