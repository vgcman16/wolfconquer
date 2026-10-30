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
			// Caller owns/disposes this independent packet after its last use.
			var name = Name;
			ArgumentNullException.ThrowIfNull(name, nameof(Name));
			var seal = LegacyNativePacket.SealString;
			if (seal is not null && seal.Length != 8)
				throw new InvalidOperationException("Trade-info packet seal is invalid.");
			var sealSize = seal is null ? 0 : 8;
			var maximumBody = LegacyNativePacket.MAX_SIZE - 4 - sealSize;
			// Necessary envelope bound only, not a game-name character limit.
			if (System.Text.Encoding.UTF8.GetByteCount(name) > maximumBody)
				throw new InvalidOperationException("Trade-info payload exceeds its packet allocation.");
			var snapshot = new TradeInfo
			{
				un1 = un1, un2 = un2, Sender = Sender, Name = name, Receiver = Receiver,
				Level = Level, BattlePower = BattlePower, un8 = un8, un9 = un9,
				Spouse = Spouse, Friend = Friend, TradePartner = TradePartner, Mentor = Mentor,
				Apprentice = Apprentice, Teammate = Teammate, GuildMember = GuildMember, Enemy = Enemy,
				un18 = un18, un19 = un19, un20 = un20,
			};
			byte[] body;
			using (var managed = new MemoryStream())
			{
				// Same runtime-object protobuf model as ProtoBufferSerialize; no native copy yet.
				Serializer.Serialize((Stream)managed, (object)snapshot);
				if (managed.Length > maximumBody)
					throw new InvalidOperationException("Trade-info frame exceeds its packet allocation.");
				body = managed.ToArray();
			}
			var declaredLength = checked((ushort)(4 + body.Length));
			var frame = new byte[checked(declaredLength + sealSize)];
			System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame, declaredLength);
			System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2396);
			body.CopyTo(frame, 4);
			// Native Write(string,8) copies the first 8 UTF8 bytes, including for non-ASCII seals.
			if (seal is not null)
				System.Text.Encoding.UTF8.GetBytes(seal).AsSpan(0, sealSize).CopyTo(frame.AsSpan(declaredLength));
			var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
			try
			{
				unsafe { System.Runtime.InteropServices.Marshal.Copy(frame, 0, (nint)packet.Memory, frame.Length); }
				packet.Size = frame.Length;
				packet.Seek(0); // Existing using-wrapper disposal resets the returned cursor to 0.
				return packet;
			}
			catch (Exception failure)
			{
				try { packet.Dispose(); }
				catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
				throw;
			}
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
