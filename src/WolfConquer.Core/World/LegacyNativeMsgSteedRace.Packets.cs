using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeMsgSteedRace
{
private void OpenGate()
	{
		GateOpened = DateTime.Now;
		GateOpen = true;
		Gate!.X = 0;
		Gate!.Y = 0;
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeActionQuery actionQuery = new LegacyNativeActionQuery();
		actionQuery.ObjId = Gate!.UID;
		actionQuery.Type = 135;
		Send(stream.ActionCreate(actionQuery));
	}

private void Send(LegacyNativePacket stream)
	{
		LegacyNativeGameClientRuntime[] values = Map!.Values;
		foreach (LegacyNativeGameClientRuntime gameClient in values)
		{
			gameClient.Send(stream);
		}
	}

private void SendData(ushort ID, uint value = 0u, uint uid = 0u)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeActionQuery actionQuery = new LegacyNativeActionQuery();
		actionQuery.ObjId = uid;
		actionQuery.Type = ID;
		actionQuery.dwParam = value;
		LegacyNativeGameClientRuntime[] values = Map!.Values;
		foreach (LegacyNativeGameClientRuntime gameClient in values)
		{
			if (uid == 0)
			{
				LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery();
				actionQuery.ObjId = gameClient.Player.UID;
				actionQuery.Type = ID;
				actionQuery.dwParam = value;
				gameClient.Send(stream.ActionCreate(pQuery));
			}
			else
			{
				gameClient.Send(stream.ActionCreate(actionQuery));
			}
		}
	}

private void Status(LegacyNativeGameClientRuntime client, int rank, int time, int award)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		stream.RaceRecordCreate(LegacyNativeMsgRaceRecord.RaceRecordTypes.AddRecord, rank, client.Player.Name, time, award);
		Send(stream.RaceRecordCreate(LegacyNativeMsgRaceRecord.RaceRecordTypes.AddRecord, rank, client.Player.Name, time, award));
	}

private int AwardPlayer(LegacyNativeGameClientRuntime client, int time, int rank)
	{
		return Math.Max(100000, 1000000 / rank - time * 2);
	}
}
