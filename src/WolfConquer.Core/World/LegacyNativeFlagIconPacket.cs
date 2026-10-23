using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeFlagIconPacket
{
    // Complete native MsgBuilder171..191 contract, all six fields required.
    [ProtoContract]
    public class MsgAuraProto
    {
        [ProtoMember(1, IsRequired = true)] public uint Type;
        [ProtoMember(2, IsRequired = true)] public uint UID;
        [ProtoMember(3, IsRequired = true)] public LegacyNativeMsgFlagIcon.ShowIcon Icon;
        [ProtoMember(4, IsRequired = true)] public uint Level;
        [ProtoMember(5, IsRequired = true)] public uint Mode;
        [ProtoMember(6, IsRequired = true)] public uint Damage;
    }

    public static void GetFlagIcon(this LegacyNativePacket stream, out MsgAuraProto proto)
    {
        proto = new MsgAuraProto();
        proto = stream.ProtoBufferDeserialize(proto);
    }

    public static LegacyNativePacket FlagIconCreate(this LegacyNativePacket stream, uint UID,
        LegacyNativeMsgFlagIcon.ShowIcon Icon, uint Level, uint Damage)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(new MsgAuraProto
        {
            Type = 3u,
            UID = UID,
            Icon = Icon,
            Level = Level,
            Mode = 30u,
            Damage = Damage
        });
        stream.Finalize(2175);
        return stream;
    }
}
