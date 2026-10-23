using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClone
{
	public static void CreateShadowClone(LegacyNativePlayerRuntime client, LegacyNativePacket stream)
	{
		client.MyClones.Add(new LegacyNativeClone(client, "ShadowClone", 3, stream));
		client.MyClones.Add(new LegacyNativeClone(client, "ShadowClone", 10003, stream));
	}

	public static void CreateShadowClone(LegacyNativePlayerRuntime client, int flag, LegacyNativePacket stream)
	{
		client.MyClones.Add(new LegacyNativeClone(client, "ShadowClone", flag, stream));
	}

	public void SendView(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
	{
		client.Player.View.SendView(GetArray(stream), me: true);
	}

	public void Send(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
	{
		client.Send(GetArray(stream));
	}
}
