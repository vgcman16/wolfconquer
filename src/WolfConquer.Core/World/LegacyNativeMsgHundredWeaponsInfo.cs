#nullable disable
using ProtoBuf;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native physical contracts. The native Process receiver remains absent.
internal static partial class LegacyNativeMsgHundredWeaponsInfo
{
    [ProtoContract]
    public class MsgHundredWeaponsInfoProto
    {
        [ProtoMember(1, IsRequired = true)]
        public ActionID Type;

        [ProtoMember(2, IsRequired = true)]
        public uint OwnerUID;

        [ProtoMember(3, IsRequired = true)]
        public WeaponInfo[] Weapons;
    }

    [ProtoContract]
    public class WeaponInfo
    {
        [ProtoMember(1, IsRequired = true)]
        public LegacyNativeMagicType.WeaponsType SubType;

        [ProtoMember(2, IsRequired = true)]
        public uint AppearancePosition;

        [ProtoMember(3, IsRequired = true)]
        public uint PositionFlags;

        [ProtoMember(4, IsRequired = true)]
        public byte Level;

        [ProtoMember(5, IsRequired = true)]
        public uint Progress;

        [ProtoMember(6, IsRequired = true)]
        public int Hitpoints;

        [ProtoMember(7, IsRequired = true)]
        public int PhysicalAttack;

        [ProtoMember(8, IsRequired = true)]
        public int PhysicalDefense;

        [ProtoMember(9, IsRequired = true)]
        public int MagicAttack;

        [ProtoMember(10, IsRequired = true)]
        public int MagicDefense;

        [ProtoMember(12, IsRequired = true)]
        public uint Score;
    }

    public enum ActionID
    {
        None,
        Update,
        RequestInfo,
        Add
    }
}
