using GameClient = WolfConquer.Core.World.LegacyNativeGameClientRuntime;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;
using MsgSchedules = WolfConquer.Core.World.LegacyNativeMsgSchedules;
using MsgMessage = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgDisCity
{
	public void KillTheUltimatePluto(GameClient client)
	{
		MsgSchedules.SendSysMesage(client.Player.Name + "~has~defeated~UltimatePluto!", MsgMessage.ChatMode.Talk);
	}
}
