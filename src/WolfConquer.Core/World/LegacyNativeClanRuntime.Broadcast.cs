using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClanRuntime
{
public void ShareBattlePower(uint leaderUID, uint BpShare, LegacyNativeGameClientRuntime client)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, client.Player.UID, 2);
		stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.ClanShareBp, leaderUID);
		stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.ClanShareBp, BpShare);
		client.Send(msgUpdate.GetArray(stream));
	}

public void Send(LegacyNativePacket data)
	{
		LegacyNativeGameClientRuntime[] array = LegacyNativePoolClients.GamePoll!.Values.ToArray();
		LegacyNativeGameClientRuntime[] array2 = array;
		foreach (LegacyNativeGameClientRuntime gameClient in array2)
		{
			if (gameClient.Player.ClanUID == ID)
			{
				gameClient.Send(data);
			}
		}
	}

public void SendBuletin(LegacyNativePacket stream, LegacyNativeGameClientRuntime client)
	{
		if (ClanBuletin != "None")
		{
			client.Send(stream.ClanBulletinCreate(client, this));
		}
	}
}
