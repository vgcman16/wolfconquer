using ProtoBuf;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeHeroInfoPackets
{
	// Entire native MsgBuilder.HeroInfoProto, all50 required fields; no player DTO.
	[ProtoContract]
	public class HeroInfoProto
	{
		[ProtoMember(1, IsRequired = true)]
		public ulong UID;

		[ProtoMember(2, IsRequired = true)]
		public ulong AparenceType;

		[ProtoMember(3, IsRequired = true)]
		public ulong Mesh;

		[ProtoMember(4, IsRequired = true)]
		public ulong Hair;

		[ProtoMember(5, IsRequired = true)]
		public long Money;

		[ProtoMember(6, IsRequired = true)]
		public uint ConquerPoints;

		[ProtoMember(7, IsRequired = true)]
		public ulong Experience;

		[ProtoMember(8, IsRequired = true)]
		public ushort ServerInfoID;

		[ProtoMember(9, IsRequired = true)]
		public ushort SetLocationType;

		[ProtoMember(10, IsRequired = true)]
		public ulong SpecialTitleID;

		[ProtoMember(11, IsRequired = true)]
		public ulong SpecialWingID;

		[ProtoMember(12, IsRequired = true)]
		public ulong VirtutePoints;

		[ProtoMember(13, IsRequired = true)]
		public ulong Strength;

		[ProtoMember(14, IsRequired = true)]
		public ulong Agility;

		[ProtoMember(15, IsRequired = true)]
		public ulong Vitality;

		[ProtoMember(16, IsRequired = true)]
		public ulong Spirit;

		[ProtoMember(17, IsRequired = true)]
		public ulong Atributes;

		[ProtoMember(18, IsRequired = true)]
		public ulong HitPoints;

		[ProtoMember(19, IsRequired = true)]
		public ulong Mana;

		[ProtoMember(20, IsRequired = true)]
		public ulong PKPoints;

		[ProtoMember(21, IsRequired = true)]
		public ulong Level;

		[ProtoMember(22, IsRequired = true)]
		public ulong Class;

		[ProtoMember(23, IsRequired = true)]
		public ulong FirstClass;

		[ProtoMember(24, IsRequired = true)]
		public ulong SecoundeClass;

		[ProtoMember(25, IsRequired = true)]
		public ulong NobilityRank;

		[ProtoMember(26, IsRequired = true)]
		public ulong Reborn;

		[ProtoMember(27, IsRequired = true)]
		public ulong inittransfer;

		[ProtoMember(28, IsRequired = true)]
		public long QuizPoints;

		[ProtoMember(29, IsRequired = true)]
		public long MainFlag;

		[ProtoMember(30, IsRequired = true)]
		public long Enilghten;

		[ProtoMember(31, IsRequired = true)]
		public long EnlightenReceive;

		[ProtoMember(32, IsRequired = true)]
		public long IDK_32;

		[ProtoMember(33, IsRequired = true)]
		public long IDK_33;

		[ProtoMember(34, IsRequired = true)]
		public long VipLevel;

		[ProtoMember(35, IsRequired = true)]
		public long MyTitle;

		[ProtoMember(36, IsRequired = true)]
		public long BoundConquerPoints;

		[ProtoMember(37, IsRequired = true)]
		public long ActiveSublass;

		[ProtoMember(38, IsRequired = true)]
		public long SubClassHashPoint;

		[ProtoMember(39, IsRequired = true)]
		public long RacePoints;

		[ProtoMember(40, IsRequired = true)]
		public long CountryID;

		[ProtoMember(41, IsRequired = true)]
		public long IDK_41;

		[ProtoMember(42, IsRequired = true)]
		public long DominoCode;

		[ProtoMember(43, IsRequired = true)]
		public long ClassExperience;

		[ProtoMember(44, IsRequired = true)]
		public long IDK_44;

		[ProtoMember(45, IsRequired = true)]
		public long IDK_45;

		[ProtoMember(46, IsRequired = true)]
		public string? Name;

		[ProtoMember(47, IsRequired = true)]
		public string? Spouse;

		[ProtoMember(48, IsRequired = true)]
		public long IDK_48;

		[ProtoMember(49, IsRequired = true)]
		public long IDK_49;

		[ProtoMember(50, IsRequired = true)]
		public string? Bayo;
	}
}
