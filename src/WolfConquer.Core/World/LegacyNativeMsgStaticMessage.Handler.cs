using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;

// Sanitized static-message response consumer using the actual player's callback claim.
internal partial struct LegacyNativeMsgStaticMessage
{
// Native [Packet(2320)]; component method only, no host receiver registration.
	public static void Handler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
	{
		ArgumentNullException.ThrowIfNull(stream);
		stream.GetStaticMessage(out var Accept);
		if (Accept != 1) return;
		ArgumentNullException.ThrowIfNull(user);
		var player = user.Player;
		ArgumentNullException.ThrowIfNull(player);
		var callback = player.ClaimMessageBoxResponse(user);
		callback?.Invoke(user);
	}
}
