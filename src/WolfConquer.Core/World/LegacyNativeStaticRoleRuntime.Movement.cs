using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStaticRoleRuntime
{
	public bool MoveTo(ushort _x, ushort _y)
	{
		if (_x == X && _y == Y)
		{
			return false;
		}
		LegacyNativePlayerMapFlags.ConquerAngle angle = LegacyNativeRoleCore.GetAngle(X, Y, _x, _y);
		ushort x = X;
		ushort y = Y;
		LegacyNativeRoleCore.IncXY(angle, ref x, ref y);
		GMap.View!.MoveTo((ILegacyNativeMapObj)this, (int)x, (int)y);
		X = x;
		Y = y;
		LegacyNativeWalkQuery pQuery = new LegacyNativeWalkQuery
		{
			Direction = (uint)angle,
			Running = 2u,
			UID = UID,
			MapID = GMap.ID
		};
		using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
		{
			LegacyNativePacket stream = recycledPacket.GetStream();
			Send(stream.MovementCreate(pQuery));
		}
		return true;
	}
}
