using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterRole
{
	public short GetMyDistance(ushort X2, ushort Y2)
	{
		return LegacyNativeRoleCore.GetDistance(X, Y, X2, Y2);
	}

	public short OldGetDistance(ushort X2, ushort Y2)
	{
		return LegacyNativeRoleCore.GetDistance(PX, PY, X2, Y2);
	}

	public bool InView(ushort X2, ushort Y2, byte distance)
	{
		return OldGetDistance(X2, Y2) >= distance && GetMyDistance(X2, Y2) < distance;
	}

	public void Send(LegacyNativePacket msg)
	{
		View.SendScreen(msg, GMap!);
	}

	public void UpdateMonsterView(LegacyNativeRoleView? Target, LegacyNativePacket stream)
	{
		foreach (ILegacyNativeMapObj item in View.Roles(GMap!, LegacyNativeMapObjectType.Player))
		{
			if (InView(item.X, item.Y, 18))
			{
				item.Send(GetArray(stream, view: false, st: false));
			}
		}
	}

	public bool UpdateMapCoords(ushort New_X, ushort New_Y, LegacyNativeGameMap _map)
	{
		if (!_map.IsFlagPresent(New_X, New_Y, LegacyNativeMapFlagType.Monster))
		{
			_map.SetMonsterOnTile(X, Y, Value: false);
			_map.SetMonsterOnTile(New_X, New_Y, Value: true);
			_map.View!.MoveTo(this, New_X, New_Y);
			X = New_X;
			Y = New_Y;
			return true;
		}
		return false;
	}

	public void SendString(LegacyNativePacket stream, LegacyNativeMsgStringPacket.StringID id, params string[] args)
	{
		LegacyNativeMsgStringPacket msgStringPacket = new LegacyNativeMsgStringPacket();
		msgStringPacket.ID = id;
		msgStringPacket.UID = UID;
		msgStringPacket.Strings = args;
		Send(stream.StringPacketCreate(msgStringPacket));
	}
}
