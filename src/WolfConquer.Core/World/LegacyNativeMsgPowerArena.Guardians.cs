using GameClient = WolfConquer.Core.World.LegacyNativeGameClientRuntime;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;
using ProcesType = WolfConquer.Core.World.LegacyNativeProcesType;
using MsgMessage = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgPowerArena
{
	public void CheckMonstersMap(Player user)
	{
		if (Process != ProcesType.Alive)
		{
			return;
		}
		user.DefeatedArenaGuardians++;
		switch (user.Map)
		{
		case 1770u:
			if (user.DefeatedArenaGuardians >= 30)
			{
				user.Owner.SendSysMesage("You have flattened enough guardians to claim experience from the Agate now.", MsgMessage.ChatMode.Talk);
			}
			break;
		case 1771u:
			if (user.DefeatedArenaGuardians >= 45)
			{
				user.Owner.SendSysMesage("You have flattened enough guardians to claim experience from the Agate now.", MsgMessage.ChatMode.Talk);
			}
			break;
		case 1772u:
			if (user.DefeatedArenaGuardians >= 60)
			{
				user.Owner.SendSysMesage("You have flattened enough guardians to claim experience from the Agate now.", MsgMessage.ChatMode.Talk);
			}
			break;
		case 1773u:
			if (user.DefeatedArenaGuardians >= 35)
			{
				user.Owner.SendSysMesage("You have flattened enough guardians to claim experience from the Agate now.", MsgMessage.ChatMode.Talk);
			}
			break;
		case 1774u:
			if (user.DefeatedArenaGuardians >= 45)
			{
				user.Owner.SendSysMesage("You have flattened enough guardians to claim experience from the Agate now.", MsgMessage.ChatMode.Talk);
			}
			break;
		case 1775u:
			if (user.DefeatedArenaGuardians >= 60)
			{
				user.Owner.SendSysMesage("You have flattened enough guardians to claim experience from the Agate now.", MsgMessage.ChatMode.Talk);
			}
			break;
		case 1777u:
			if (user.DefeatedArenaGuardians >= 90)
			{
				user.Owner.SendSysMesage("You have flattened enough guardians to claim experience from the Agate now.", MsgMessage.ChatMode.Talk);
			}
			break;
		case 1776u:
			break;
		}
	}

	public bool InPowerArena(GameClient user)
	{
		return user.Player.Map == 1770 || user.Player.Map == 1771 || user.Player.Map == 1772 || user.Player.Map == 1773 || user.Player.Map == 1774 || user.Player.Map == 1775 || user.Player.Map == 1777;
	}
}
