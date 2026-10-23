
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal class LegacyNativeMsgJiangPkMode
{
    // Whole native component body, deliberately unregistered until actual client adoption.
	internal static void Process(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
	{
		stream.GetJiangPkMode(out var Flag);
		user.Player.JiangPkFlag = Flag;
	}
}
