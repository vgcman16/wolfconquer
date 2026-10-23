using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgAttackPacket
{
	public static void Interaction(this LegacyNativePacket stream, out LegacyNativeInteractQuery pQuery, LegacyNativeGameClientRuntime client)
	{
		pQuery = new LegacyNativeInteractQuery();
		pQuery = stream.ProtoBufferDeserialize(pQuery);
		if (pQuery.AtkType == 24 && !client.OnAutoAttack)
		{
			DecodeMagicAttack(pQuery, client);
		}
	}
}
