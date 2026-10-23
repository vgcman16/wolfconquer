using System;
using System.Linq;
using System.Runtime.InteropServices;

using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Explicit)]
internal struct LegacyNativeMsgJiangHuRank
{
    // Whole native component body, deliberately unregistered until actual client adoption.
	internal static void Process(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
	{
		stream.GetJiangHuRank(out var Page);
		Page--;
		LegacyNativeJiangHu[] array = LegacyNativeJiangHu.JiangHuRanking.TopRank100!.ToArray();
		if (array == null)
		{
			return;
		}
		int num = Page * 10;
		int num2 = Math.Min(10, Math.Max(0, array.Length - num));
		byte myRank = 0;
		if (user.Player.MyJiangHu != null && user.Player.MyJiangHu.Rank < 100)
		{
			myRank = user.Player.MyJiangHu.Rank;
		}
		stream.JiangHuRankCreate((byte)(Page + 1), myRank, (byte)num2, (byte)Math.Min(100, array.Length));
		for (int i = 0; i < num2; i++)
		{
			if (array.Length > num + i)
			{
				LegacyNativeJiangHu jiangHu = array[num + i];
				if (jiangHu.Rank < 100)
				{
					stream.AddItemJiangHuRank(jiangHu);
				}
			}
		}
		user.Send(stream.JiangHuRankFinalize());
	}
}
