using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Entire native AzureShield owner, no host or attack-dispatch stand-in.
internal class LegacyNativeAzureShield
{
	public static void CreateDmg(LegacyNativePlayerRuntime player, LegacyNativePlayerRuntime target, uint DMG)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeInteractQuery pQuery = new LegacyNativeInteractQuery
		{
			AtkType = 55,
			UID = player.UID,
			OpponentUID = target.UID,
			X = target.X,
			Y = target.Y,
			Damage = (int)DMG
		};
		target.View.SendView(stream.InteractionCreate(pQuery), me: true);
	}
}
