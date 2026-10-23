using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native CheckItems owner, no host or attack-dispatch stand-in.
internal class LegacyNativeCheckItems
{
	public static void AttackDurability(LegacyNativeGameClientRuntime client, LegacyNativePacket? stream)
	{
		if (!client.Player.Rate(4))
		{
		}
	}

	public static void RespouseDurability(LegacyNativeGameClientRuntime client)
	{
		if (!client.Player.Rate(4))
		{
		}
	}
}
