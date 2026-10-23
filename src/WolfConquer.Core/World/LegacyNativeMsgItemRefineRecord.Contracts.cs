using ProtoBuf;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgItemRefineRecord
{
    [ProtoContract]
    public class ItemRefineRecord
    {
        [ProtoMember(1, IsRequired = true)] public uint Type;
        [ProtoMember(2)] public uint Points;
        [ProtoMember(3, IsRequired = true)] public uint UnKnow;
        [ProtoMember(4, IsRequired = false)] public uint Unlocked;
        [ProtoMember(5, IsRequired = false)] public RefineRecord[]? msgs;
    }

    [ProtoContract]
    public class RefineRecord
    {
        [ProtoMember(1, IsRequired = true)] public uint Index;
        [ProtoMember(2, IsRequired = true)] public uint ItemUID;
        [ProtoMember(3, IsRequired = true)] public uint ItemID;
        [ProtoMember(4, IsRequired = true)] public uint PurificationID;
        [ProtoMember(5, IsRequired = true)] public uint OpenStars;
        [ProtoMember(6, IsRequired = true)] public uint RequestStars;
        [ProtoMember(7, IsRequired = true)] public string? str;
    }
}
