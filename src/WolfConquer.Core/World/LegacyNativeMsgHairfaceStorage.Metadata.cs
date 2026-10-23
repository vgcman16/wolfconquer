using ProtoBuf;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgHairfaceStorage
{
	[ProtoContract]
	public class MsgHairfaceStorageProto
	{
		[ProtoMember(1, IsRequired = true)]
		public LegacyHairFaceStorageAction Type;

		[ProtoMember(2)]
		public Operation? Hair;

		[ProtoMember(3)]
		public Operation? HairColor;

		[ProtoMember(4)]
		public Item[]? AvatarsOrHairTap;

		[ProtoMember(5)]
		public Item[]? Items;
	}

	[ProtoContract]
	public class Item
	{
		[ProtoMember(1, IsRequired = true)]
		public LegacyHairFaceStorageType Type;

		[ProtoMember(2, IsRequired = true)]
		public uint ID;

		[ProtoMember(3, IsRequired = true)]
		public bool Equipped;

		[ProtoMember(4, IsRequired = true)]
		public uint Unk4;

		[ProtoMember(5)]
		public long[]? Colors;
	}

	[ProtoContract]
	public class Operation
	{
		[ProtoMember(1, IsRequired = true)]
		public uint ID;

		[ProtoMember(2, IsRequired = true)]
		public uint dwParam;
	}


}
