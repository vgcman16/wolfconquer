using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;

// Whole source static-message response consumer. Source delegates, not test adapters.
internal partial struct LegacyNativeMsgStaticMessage
{
// Native [Packet(2320)]; component method only, no host receiver registration.
	public static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
	{
		stream.GetStaticMessage(out var Accept);
		if (Accept != 1 || LegacyNativeConstants.BlockTeleportMap.Contains(user.Player.Map))
		{
			return;
		}
		if (user.Player.StartMessageBox > DateTime.Now)
		{
			if (user.Player.MessageOK != null)
			{
				user.Player.MessageOK(user);
			}
			else if (user.Player.MessageCancel != null)
			{
				user.Player.MessageCancel(user);
			}
		}
		user.Player.MessageOK = null;
		user.Player.MessageCancel = null;
	}
}
