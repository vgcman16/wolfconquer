using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole physical native SobNpc owner/contract; Die remains a separate held lifecycle.
internal partial class LegacyNativeSobNpcRuntime : ILegacyNativeMapObj
{
	[ProtoContract]
	public class SobNpcSpawnProto
	{
		[ProtoMember(1, IsRequired = true)]
		public uint UID;

		[ProtoMember(2, IsRequired = true)]
		public uint OwnerUID;

		[ProtoMember(3, IsRequired = true)]
		public uint Unknown2;

		[ProtoMember(4, IsRequired = true)]
		public uint MaxHitpoints;

		[ProtoMember(5, IsRequired = true)]
		public uint Hitpoints;

		[ProtoMember(6, IsRequired = true)]
		public ushort X;

		[ProtoMember(7, IsRequired = true)]
		public ushort Y;

		[ProtoMember(8, IsRequired = true)]
		public ushort Mesh;

		[ProtoMember(9, IsRequired = true)]
		public byte Type;

		[ProtoMember(10, IsRequired = true)]
		public ushort Sort;

		[ProtoMember(12)]
		public string? Name;
	}

	public enum StaticMesh : ushort
	{
		Vendor = 406,
		LeftGate = 1551,
		OpenLeftGate = 1511,
		RightGate = 1527,
		OpenRightGate = 1537,
		Pole = 1137,
		SuperGuildWarPole = 31220
	}

	public LegacyNativeStatue? statue = null;

	public LegacyNativeStatusFlagsBigVector32 BitVector;

	public uint GuildID;

	public const byte SeedDistrance = 19;

	private int Hit;

	public StaticMesh Mesh;

	public LegacyNativeNpcType Type;

	public ushort Sort;

	public string? Name;

	public string? Name2;

	public LegacyNativeBooth? Booth;

	public LegacyNativeGameClientRuntime? OwnerVendor;

	public bool AllowDynamic { get; set; }

	public uint IndexInScreen { get; set; }

	public bool IsStatue => statue != null;

	public uint UID { get; set; }

	public int MaxHitPoints { get; set; }

	public int HitPoints
	{
		get
		{
			return Hit;
		}
		set
		{
			Hit = value;
		}
	}

	public ushort X { get; set; }

	public ushort Y { get; set; }

	public uint Map { get; set; }

	public uint DynamicID { get; set; }

	public bool Alive => HitPoints > 0;

	public LegacyNativeMapObjectType ObjType { get; set; }


}
