using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
	public void SendAutoPatcher(uint map, ushort x, ushort y, uint NpcUid)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
		{
			ObjId = Player.UID,
			Type = 162,
			NpcID = (int)NpcUid,
			PositionX = x,
			PositionY = y,
			dwParam = map
		};
		Player.Send(stream.ActionCreate(pQuery));
	}
}
