#nullable disable
using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native NpcReply physical declarations; both whole codecs are in their partial.
internal static partial class LegacyNativeNpcReply
{
    [ProtoContract]
    public class NpcRequestProto
    {
        [ProtoMember(1, IsRequired = true)] public ushort Action;
        [ProtoMember(2, IsRequired = true)] public uint NpcID;
        [ProtoMember(3, IsRequired = true)] public byte OptionID;
        [ProtoMember(4, IsRequired = true)] public byte InteractType;
        [ProtoMember(5)] public string Input;
    }

    public enum InteractTypes : byte
    {
        Dialog = 1,
        Option = 2,
        Input = 3,
        Avatar = 4,
        MessageBox = 6,
        Finish = 100,
        Page = 113
    }
}
