using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public LegacyNativeGameClientRuntime[] Values
	{
		get
		{
			if (Update)
			{
				Users = Clients.Values.ToArray();
				Update = false;
			}
			return Users;
		}
		set
		{
		}
	}

	public void Enquer(LegacyNativeGameClientRuntime client)
	{
		if (Clients.TryAdd(client.Player.UID, client))
		{
			View.EnterMap((ILegacyNativeMapObj)client.Player);
			client.Map = this;
			Update = true;
		}
	}

	public void Denquer(LegacyNativeGameClientRuntime client)
	{
		if (Clients.TryRemove(client.Player.UID, out var _))
		{
			View.LeaveMap((ILegacyNativeMapObj)client.Player);
			Update = true;
		}
	}

	public void SendSysMesage(string Messaj, LegacyNativeMsgMessage.ChatMode ChatType = LegacyNativeMsgMessage.ChatMode.TopLeft, LegacyNativeMsgMessage.MsgColor color = LegacyNativeMsgMessage.MsgColor.red)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativePacket array = new LegacyNativeMsgMessage(Messaj, color, ChatType).GetArray(stream);
		LegacyNativeGameClientRuntime[] users = Users;
		foreach (LegacyNativeGameClientRuntime gameClient in users)
		{
			gameClient.Send(array);
		}
	}
}
