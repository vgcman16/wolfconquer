using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgBuilder GenericRanking reader, writer, all three entry
// overloads and finalizer. No replacement snapshot or Process2165 dispatch.
internal static class LegacyNativeGenericRankingPacket
{
	public static void GetGenericRanking(this LegacyNativePacket stream, out LegacyNativeMsgGenericRanking.Action Mode, out LegacyNativeMsgGenericRanking.RankType ranktyp, out ushort RegisteredCount, out ushort Page, out int Count)
	{
		Mode = (LegacyNativeMsgGenericRanking.Action)stream.ReadUInt8();
		ranktyp = (LegacyNativeMsgGenericRanking.RankType)stream.ReadUInt32();
		RegisteredCount = stream.ReadUInt16();
		Page = stream.ReadUInt8();
		stream.ReadUInt8();
		Count = stream.ReadInt32();
	}

	public static LegacyNativePacket GenericRankingCreate(this LegacyNativePacket stream, LegacyNativeMsgGenericRanking.Action Mode, LegacyNativeMsgGenericRanking.RankType ranktyp, ushort RegisteredCount, ushort Page, int Count, byte Finish = 0)
	{
		stream.InitWriter();
		stream.Write((byte)Mode);
		stream.Write((uint)ranktyp);
		stream.Write(RegisteredCount);
		stream.Write((byte)Page);
		stream.Write(Finish);
		stream.Write(Count);
		stream.Write(0u);
		return stream;
	}

	public static LegacyNativePacket AddItemGenericRankingCreate(this LegacyNativePacket stream, int Rank, uint Amount, uint UID, string name)
	{
		stream.Write((byte)Rank);
		stream.Write((long)Amount);
		stream.Write(UID);
		stream.Write(UID);
		stream.Write(name, 32);
		stream.Write(name, 32);
		stream.ZeroFill(28);
		return stream;
	}

	public static LegacyNativePacket AddItemGenericRankingCreate(this LegacyNativePacket stream, int Rank, uint Amount, uint UID, string name, uint Level, uint Class, uint Mesh)
	{
		stream.Write((byte)Rank);
		stream.Write((long)Amount);
		stream.Write(UID);
		stream.Write(UID);
		stream.Write(name, 32);
		stream.Write(name, 32);
		stream.Write(Level);
		stream.Write(Class);
		stream.Write(Mesh);
		stream.ZeroFill(16);
		return stream;
	}

	public static LegacyNativePacket AddItemGenericRankingCreate(this LegacyNativePacket stream, int Rank, uint Amount, uint UID1, uint UID2, string name, uint Level, uint Class, uint Mesh)
	{
		stream.Write((byte)Rank);
		stream.Write((long)Amount);
		stream.Write(UID1);
		stream.Write(UID2);
		stream.Write(name, 32);
		stream.Write(name, 32);
		stream.Write(Level);
		stream.Write(Class);
		stream.Write(Mesh);
		stream.ZeroFill(16);
		return stream;
	}

	public static LegacyNativePacket GenericRankingFinalize(this LegacyNativePacket stream, bool finish = true, int count = 0)
	{
		if (!finish)
		{
			int position = stream.Position;
			stream.Seek(12);
			stream.Write((byte)1);
			stream.Write(count);
			stream.Seek(position);
		}
		stream.Finalize(2165);
		return stream;
	}
}
