using ProtoBuf;

namespace WolfConquer.Core.World;

// Whole native MsgBuilder208..234 required8-field contract.
	[ProtoContract]
	internal class LegacyNativeMsgDataProto
	{
		[ProtoMember(1, IsRequired = true)]
		public uint Type;

		[ProtoMember(2, IsRequired = true)]
		public int Year;

		[ProtoMember(3, IsRequired = true)]
		public int Month;

		[ProtoMember(4, IsRequired = true)]
		public int DayOfYear;

		[ProtoMember(5, IsRequired = true)]
		public int DayOfMonth;

		[ProtoMember(6, IsRequired = true)]
		public int Hour;

		[ProtoMember(7, IsRequired = true)]
		public int Minute;

		[ProtoMember(8, IsRequired = true)]
		public int Second;
	}
