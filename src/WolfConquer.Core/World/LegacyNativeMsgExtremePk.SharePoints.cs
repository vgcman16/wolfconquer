using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeMsgExtremePk
{
	public void SharePoints(LegacyNativeGameClientRuntime user, LegacyNativeGameClientRuntime Killer)
	{
		uint num = user.Player.XtremePkPoints / 5;
		Killer.Player.XtremePkPoints += num;
		user.Player.XtremePkPoints -= num;
		Killer.SendSysMesage("You gained " + num + " points for killing " + user.Player.Name, LegacyNativeMsgMessage.ChatMode.Center, LegacyNativeMsgMessage.MsgColor.white);
		user.SendSysMesage("You lost " + num + " points when " + Killer.Player.Name.ToString() + " has killed you.", LegacyNativeMsgMessage.ChatMode.Center, LegacyNativeMsgMessage.MsgColor.white);
	}
}
