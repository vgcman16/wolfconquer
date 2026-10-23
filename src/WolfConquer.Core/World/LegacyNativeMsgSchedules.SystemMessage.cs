using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeMsgSchedules
{
internal static void SendSysMesage(string Messaj, LegacyNativeMsgMessage.ChatMode ChatType = LegacyNativeMsgMessage.ChatMode.TopLeft, LegacyNativeMsgMessage.MsgColor color = LegacyNativeMsgMessage.MsgColor.red, bool SendScren = false)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativePacket array = new LegacyNativeMsgMessage(Messaj, color, ChatType).GetArray(stream);
		foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
		{
			value.Send(array);
		}
	}
}
