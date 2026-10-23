using ProtoBuf;
using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;

// Entire native contracts, enum and codecs. Whole receiver remains unregistered/held.
internal static partial class LegacyNativeMsgTrade
{
	[ProtoContract]
	public class Trade
	{
		[ProtoMember(1, IsRequired = true)]
		public ulong Action;

		[ProtoMember(2, IsRequired = true)]
		public long Money;

		[ProtoMember(3, IsRequired = true)]
		public ulong ItemUID;

		[ProtoMember(4, IsRequired = true)]
		public uint CPS;

		[ProtoMember(5, IsRequired = true)]
		public ulong Unknown;

		[ProtoMember(6, IsRequired = true)]
		public ulong UID;
	}

	[ProtoContract]
	public class TradeInfo
	{
		[ProtoMember(1, IsRequired = true)]
		public bool un1;

		[ProtoMember(2, IsRequired = true)]
		public uint un2;

		[ProtoMember(3, IsRequired = true)]
		public uint Sender;

		[ProtoMember(4, IsRequired = true)]
		public string? Name;

		[ProtoMember(5, IsRequired = true)]
		public uint Receiver;

		[ProtoMember(6, IsRequired = true)]
		public uint Level;

		[ProtoMember(7, IsRequired = true)]
		public uint BattlePower;

		[ProtoMember(8, IsRequired = true)]
		public uint un8;

		[ProtoMember(9, IsRequired = true)]
		public uint un9;

		[ProtoMember(10, IsRequired = true)]
		public bool Spouse;

		[ProtoMember(11, IsRequired = true)]
		public bool Friend;

		[ProtoMember(12, IsRequired = true)]
		public bool TradePartner;

		[ProtoMember(13, IsRequired = true)]
		public bool Mentor;

		[ProtoMember(14, IsRequired = true)]
		public bool Apprentice;

		[ProtoMember(15, IsRequired = true)]
		public bool Teammate;

		[ProtoMember(16, IsRequired = true)]
		public bool GuildMember;

		[ProtoMember(17, IsRequired = true)]
		public bool Enemy;

		[ProtoMember(18, IsRequired = true)]
		public uint un18;

		[ProtoMember(19, IsRequired = true)]
		public uint un19;

		[ProtoMember(20, IsRequired = true)]
		public long un20;

		public LegacyNativePacket ToArray()
		{
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			stream.InitWriter();
			stream.ProtoBufferSerialize(this);
			stream.Finalize(2396);
			return stream;
		}
	}

	public enum TradeID : uint
	{
		Request,
		AcceptRequest,
		Close,
		ShowTable,
		Finish,
		HideTable,
		AddItem,
		SetMoney,
		ShowMoney,
		Accept,
		RemoveItem,
		SetCPS,
		ShowCPS
	}

	public static LegacyNativePacket CreateTradeInfo(this LegacyNativePacket stream, Trade obj)
	{
		stream.InitWriter();
		stream.ProtoBufferSerialize(obj);
		stream.Finalize(2119);
		return stream;
	}

	public static void GetTradeInfo(this LegacyNativePacket stream, out Trade pQuery)
	{
		pQuery = new Trade();
		pQuery = stream.ProtoBufferDeserialize(pQuery);
	}

}
