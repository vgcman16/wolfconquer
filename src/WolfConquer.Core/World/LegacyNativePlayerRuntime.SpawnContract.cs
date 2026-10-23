using ProtoBuf;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Complete native Player.SpawnPacketProto metadata used by the whole native
    // GetArray/map contract. This does not adopt the player into a live host.
    [ProtoContract]
    public class SpawnPacketProto
    {
        [ProtoMember(1, IsRequired = true)]
        public uint Mesh;

        [ProtoMember(2, IsRequired = true)]
        public uint UID;

        [ProtoMember(3, IsRequired = true)]
        public uint GuildID;

        [ProtoMember(4, IsRequired = true)]
        public uint GuildRank;

        [ProtoMember(5, IsRequired = true)]
        public uint Unknown;

        [ProtoMember(6, IsRequired = true)]
        public ulong[] StatusFlags = new ulong[8];

        [ProtoMember(7)]
        public ushort AppearanceType;

        [ProtoMember(8, IsRequired = true)]
        public uint Head;

        [ProtoMember(9, IsRequired = true)]
        public uint Garment;

        [ProtoMember(10, IsRequired = true)]
        public uint Armor;

        [ProtoMember(11, IsRequired = true)]
        public uint LeftWeapon;

        [ProtoMember(12, IsRequired = true)]
        public uint RightWeapon;

        [ProtoMember(13)]
        public uint LeftWeaponAccessory;

        [ProtoMember(14)]
        public uint RightWeaponAccessory;

        [ProtoMember(15)]
        public uint Steed;

        [ProtoMember(16)]
        public uint MountArmor;

        [ProtoMember(17)]
        public uint Wing;

        [ProtoMember(18)]
        public uint WingPlus;

        [ProtoMember(19)]
        public uint WingProgress;

        [ProtoMember(20)]
        public uint Bottle;

        [ProtoMember(21)]
        public uint Hitpoints;

        [ProtoMember(23)]
        public uint MaxLife;

        [ProtoMember(26, IsRequired = true)]
        public byte MonsterLevel;

        [ProtoMember(27, IsRequired = true)]
        public ushort X;

        [ProtoMember(28, IsRequired = true)]
        public ushort Y;

        [ProtoMember(29, IsRequired = true)]
        public uint HairStyle;

        [ProtoMember(30, IsRequired = true)]
        public byte Facing;

        [ProtoMember(31, IsRequired = true)]
        public ushort Action;

        [ProtoMember(34, IsRequired = true)]
        public byte Reborn;

        [ProtoMember(35, IsRequired = true)]
        public ushort Level;

        [ProtoMember(36, IsRequired = true)]
        public bool WindowSpawn;

        [ProtoMember(37, IsRequired = true)]
        public bool Away;

        [ProtoMember(38, IsRequired = true)]
        public uint ExtraBattlePower;

        [ProtoMember(42, IsRequired = true)]
        public uint FlowerIcon;

        [ProtoMember(43, IsRequired = true)]
        public byte NobilityRank;

        [ProtoMember(44)]
        public uint QuizPoints;

        [ProtoMember(45)]
        public uint SteedPlus;

        [ProtoMember(46)]
        public uint SteedColor;

        [ProtoMember(48)]
        public uint Enlighten;

        [ProtoMember(49)]
        public uint InnerStrengthScore;

        [ProtoMember(53)]
        public uint ClanUID;

        [ProtoMember(54)]
        public ushort ClanRank;

        [ProtoMember(56)]
        public uint Title;

        [ProtoMember(57)]
        public byte PokerSeat;

        [ProtoMember(58)]
        public uint PokerTableID;

        [ProtoMember(59)]
        public uint GuildBattlePower;

        [ProtoMember(61)]
        public bool RaceItem;

        [ProtoMember(63, IsRequired = true)]
        public bool Boss;

        [ProtoMember(64, IsRequired = true)]
        public uint HeadSoul;

        [ProtoMember(65, IsRequired = true)]
        public uint ArmorSoul;

        [ProtoMember(66, IsRequired = true)]
        public uint LeftWeaponSoul;

        [ProtoMember(67, IsRequired = true)]
        public uint RightWeaponSoul;

        [ProtoMember(68, IsRequired = true)]
        public uint SubClass;

        [ProtoMember(69, IsRequired = true)]
        public byte ActiveSubClasses;

        [ProtoMember(70)]
        public uint FirstRebornClass;

        [ProtoMember(71)]
        public uint SecondRebornClass;

        [ProtoMember(72)]
        public uint Class;

        [ProtoMember(73)]
        public ushort CountryCode;

        [ProtoMember(74)]
        public ushort Team;

        [ProtoMember(75)]
        public int BattlePower;

        [ProtoMember(76)]
        public byte JiangHuTalent;

        [ProtoMember(77)]
        public bool JiangHuActive;

        [ProtoMember(79, IsRequired = true)]
        public ushort ServerID;

        [ProtoMember(81)]
        public uint OwnerPet;

        [ProtoMember(82)]
        public uint OwnerPet1;

        [ProtoMember(83)]
        public uint OwnerUID;

        [ProtoMember(84)]
        public uint UnionID;

        [ProtoMember(85)]
        public uint UnionExploits;

        [ProtoMember(86)]
        public uint Official_Harem_Guards;

        [ProtoMember(87)]
        public uint UnionRank;

        [ProtoMember(88)]
        public uint UnionType;

        [ProtoMember(89)]
        public uint MyTitle;

        [ProtoMember(90)]
        public uint MyTitleScore;

        [ProtoMember(91)]
        public uint MyWing;

        [ProtoMember(92)]
        public byte MainFlag;

        [ProtoMember(96)]
        public byte Relic;

        [ProtoMember(107, IsRequired = true)]
        public string[] Names = new string[7];

        [ProtoMember(108, IsRequired = true)]
        public bool UnknownBool;

        [ProtoMember(109, IsRequired = true)]
        public uint[] HundredWeapons;

        [ProtoMember(110, IsRequired = true)]
        public byte PrestigeRank;

        [ProtoMember(111, IsRequired = true)]
        public uint MonsterID;

        [ProtoMember(112, IsRequired = true)]
        public uint Test1;

        [ProtoMember(113, IsRequired = true)]
        public uint Test2;

        [ProtoMember(121, IsRequired = true)]
        public uint CollectionID;
    }

}
