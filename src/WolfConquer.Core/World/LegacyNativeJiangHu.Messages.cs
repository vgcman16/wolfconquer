using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	internal void SendInfo(LegacyNativeGameClientRuntime client, LegacyNativeMsgJiangHuInfo.JiangMode Action = LegacyNativeMsgJiangHuInfo.JiangMode.CreateJiang, bool Screen = false, params string[] data)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		stream.JiangHuInfoCreate(Action, data);
		if (Screen)
		{
			client.Player.View.SendView(stream, me: true);
		}
		else
		{
			client.Send(stream);
		}
	}

	internal void SendStatus(LegacyNativePacket stream, LegacyNativeGameClientRuntime client, LegacyNativeGameClientRuntime Attacked)
	{
		try
		{
			IEnumerable<Stage> source = ArrayStages.Where((Stage p) => p.Activate);
			byte stage = (byte)source.Count();
			uint num = 0u;
			LegacyNativeJiangHuPacket.JiangHuStatusCreate(Timer: (client.Player.UID == Attacked.Player.UID) ? 256000000u : 13751297u, stream: stream, Name: CustomizedName, Stage: stage, Talent: Talent, StudyPoints: Attacked.Player.SubClass!.StudyPoints, FreeTimeToday: FreeTimeToday, FreeTimeTodeyUsed: 0, RoundBuyPoints: RoundBuyPoints, array: source.ToArray());
			client.Send(stream);
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
