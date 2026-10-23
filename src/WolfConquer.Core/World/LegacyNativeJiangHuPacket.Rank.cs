using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgBuilder codecs; no modern frame/router or host adoption is installed.
internal static partial class LegacyNativeJiangHuPacket
{
	public static void GetJiangHuRank(this LegacyNativePacket msg, out byte Page)
	{
		byte b = msg.ReadUInt8();
		Page = msg.ReadUInt8();
	}

	public static LegacyNativePacket JiangHuRankCreate(this LegacyNativePacket stream, byte Page, byte MyRank, byte PageRegistred, byte Registred)
	{
		stream.InitWriter();
		stream.Write((byte)0);
		stream.Write(Page);
		stream.Write(MyRank);
		stream.Write(PageRegistred);
		stream.Write(Registred);
		return stream;
	}

	public static LegacyNativePacket AddItemJiangHuRank(this LegacyNativePacket stream, LegacyNativeJiangHu jiang)
	{
		stream.Write(jiang.Rank);
		stream.Write(jiang.Inner_Strength);
		stream.Write((uint)jiang.Level);
		stream.Write(jiang.Name, 32);
		stream.Write(jiang.CustomizedName, 32);
		return stream;
	}

	public static LegacyNativePacket JiangHuRankFinalize(this LegacyNativePacket stream)
	{
		stream.Finalize(2073);
		return stream;
	}
}
