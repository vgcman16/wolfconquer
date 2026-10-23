using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClanRuntime
{
public bool RemoveMember(LegacyNativeGameClientRuntime client)
	{
		if (Members.TryRemove(client.Player.UID, out var _))
		{
			client.Player.MyClanMember = null;
			client.Player.MyClan = null;
			client.Player.ClanName = "";
			client.Player.ClanRank = 0;
			client.Player.ClanUID = 0u;
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				client.Send(stream.ClanCallBackCreate(LegacyNativeMsgClan.Info.Quit, 0u, 0u, null));
			}
			ShareBattlePower(0u, 0u, client);
			client.Player.ClanBp = 0u;
			return true;
		}
		return false;
	}
}
