using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgBuilder codecs; no modern frame/router or host adoption is installed.
internal static partial class LegacyNativeJiangHuPacket
{
	public static LegacyNativePacket JiangHuInfoCreate(this LegacyNativePacket stream, LegacyNativeMsgJiangHuInfo.JiangMode Mode, params string[] items)
	{
		stream.InitWriter();
		stream.Write((byte)Mode);
		stream.Write(items);
		stream.Finalize(2082);
		return stream;
	}

	public static void GetJiangHuInfo(this LegacyNativePacket msg, out LegacyNativeMsgJiangHuInfo.JiangMode Mode, out string[] list)
	{
		Mode = (LegacyNativeMsgJiangHuInfo.JiangMode)msg.ReadUInt8();
		list = msg.ReadStringList();
	}

	public static void GetJiangPkMode(this LegacyNativePacket stream, out LegacyNativeJiangHu.AttackFlag Flag)
	{
		Flag = (LegacyNativeJiangHu.AttackFlag)stream.ReadUInt32();
	}
}
