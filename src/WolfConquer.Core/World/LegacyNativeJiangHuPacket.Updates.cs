using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgBuilder codecs; no modern frame/router or host adoption is installed.
internal static partial class LegacyNativeJiangHuPacket
{
	public static void GetJiangHuUpdates(this LegacyNativePacket stream, out LegacyNativeMsgJiangHuUpdates.Action Action, out byte Star, out byte Stage)
	{
		uint num = stream.ReadUInt32();
		byte b = stream.ReadUInt8();
		Action = (LegacyNativeMsgJiangHuUpdates.Action)stream.ReadUInt8();
		Star = stream.ReadUInt8();
		Stage = stream.ReadUInt8();
	}

	public static LegacyNativePacket JiangHuUpdatesCreate(this LegacyNativePacket stream, uint FreeCourse, LegacyNativeMsgJiangHuUpdates.Action Action, byte Star, byte Stage, ushort Atribut, byte FreeTimeTodeyUsed, uint RoundBuyPoints)
	{
		stream.InitWriter();
		stream.Write(FreeCourse);
		stream.Write((byte)0);
		stream.Write((byte)Action);
		stream.Write(Star);
		stream.Write(Stage);
		stream.Write((byte)0);
		stream.Write(Atribut);
		stream.Write(FreeTimeTodeyUsed);
		stream.Write(RoundBuyPoints);
		stream.Finalize(2070);
		return stream;
	}
}
