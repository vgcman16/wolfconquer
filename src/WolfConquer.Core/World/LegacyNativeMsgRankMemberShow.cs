#nullable disable
using System;
using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native contracts; the unrelated native Process receiver is not installed.
internal static partial class LegacyNativeMsgRankMemberShow
{
    [Flags]
    public enum Mode : uint
    {
        Show = 0u,
        Viewer = 1u
    }

    [ProtoContract]
    public class MsgRankMemberShowProto
    {
        [ProtoContract]
        public class Entry
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Type;

            [ProtoMember(2, IsRequired = true)]
            public uint Rank;

            [ProtoMember(3, IsRequired = true)]
            public uint EntityUID;

            [ProtoMember(4, IsRequired = true)]
            public string Name = "";

            [ProtoMember(5, IsRequired = true)]
            public string GuildName = "";

            [ProtoMember(6, IsRequired = true)]
            public uint Mesh;

            [ProtoMember(7, IsRequired = true)]
            public uint HairStyle;

            [ProtoMember(8, IsRequired = true)]
            public uint Head;

            [ProtoMember(9, IsRequired = true)]
            public uint Garment;

            [ProtoMember(10, IsRequired = true)]
            public uint LeftWeapon;

            [ProtoMember(11, IsRequired = true)]
            public uint LefttWeaponAccessory;

            [ProtoMember(12, IsRequired = true)]
            public uint RightWeapon;

            [ProtoMember(13, IsRequired = true)]
            public uint RightWeaponAccessory;

            [ProtoMember(14, IsRequired = true)]
            public uint MountArmor;

            [ProtoMember(15, IsRequired = true)]
            public uint Armor;

            [ProtoMember(16, IsRequired = true)]
            public uint Wing;

            [ProtoMember(17, IsRequired = true)]
            public uint WingPlus;

            [ProtoMember(18, IsRequired = true)]
            public uint Title;

            [ProtoMember(19, IsRequired = true)]
            public uint Flag;

            [ProtoMember(20, IsRequired = true)]
            public uint Profession;

            [ProtoMember(21, IsRequired = true)]
            public uint WeaponWar;
        }

        [ProtoMember(1, IsRequired = true)]
        public uint Type;

        [ProtoMember(2, IsRequired = true)]
        public Entry[] Items;
    }
}
