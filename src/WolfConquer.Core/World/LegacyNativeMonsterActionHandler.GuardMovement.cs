using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterActionHandler
{
	public void CheckGuardPosition(LegacyNativePlayerRuntime client, LegacyNativeMonsterRole monster)
	{
		if (monster.Alive && (monster.X != monster.RespawnX || monster.Y != monster.RespawnY) && DateTime.Now > monster.MoveStamp.AddMilliseconds(300.0))
		{
			monster.MoveStamp = DateTime.Now;
			LegacyNativePlayerMapFlags.ConquerAngle angle = GetAngle(monster.X, monster.Y, monster.RespawnX, monster.RespawnY);
			ushort x = monster.X;
			ushort y = monster.Y;
			IncXY(angle, ref x, ref y);
			client.Owner.Map!.View!.MoveTo((ILegacyNativeMapObj)monster, (int)x, (int)y);
			monster.X = x;
			monster.Y = y;
			LegacyNativeWalkQuery pQuery = new LegacyNativeWalkQuery
			{
				Direction = (uint)angle,
				Running = 1u,
				UID = monster.UID,
				MapID = monster.Map
			};
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			monster.Send(stream.MovementCreate(pQuery));
		}
	}
}
