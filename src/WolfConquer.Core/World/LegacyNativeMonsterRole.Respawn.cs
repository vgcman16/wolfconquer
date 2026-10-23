using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterRole
{
	public bool CanRespawn(LegacyNativeGameMap map)
	{
		DateTime now = DateTime.Now;
		if (now > RespawnStamp && !map.MonsterOnTile(RespawnX, RespawnY))
		{
			return true;
		}
		return false;
	}

	public void Respawn(bool SendEffect = true)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		ClearFlags();
		HitPoints = (uint)Family.MaxHealth;
		State = LegacyNativeMobStatus.Idle;
		LegacyNativeActionQuery actionQuery = new LegacyNativeActionQuery
		{
			ObjId = UID,
			Type = 135
		};
		Send(stream.ActionCreate(actionQuery));
		Send(GetArray(stream, view: false, st: false));
		if (SendEffect)
		{
			actionQuery.Type = 134;
			Send(stream.ActionCreate(actionQuery));
		}
		if (Family.MaxHealth > 65535)
		{
			LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
			stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.MaxHitpoints, Family.MaxHealth);
			Send(msgUpdate.GetArray(stream));
			stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, HitPoints);
			Send(msgUpdate.GetArray(stream));
		}
	}

	public void AddFadeAway(int time, LegacyNativeGameMap? map)
	{
		if (!Alive)
		{
			DateTime now = DateTime.Now;
			if (now > DeadStamp.AddSeconds(5.0) && AddFlag(LegacyNativeMsgUpdate.Flags.FadeAway, 2592000, RemoveOnDead: true))
			{
				FadeAway = now;
			}
		}
	}

	public bool RemoveView(int time, LegacyNativeGameMap map)
	{
		if (ContainFlag(LegacyNativeMsgUpdate.Flags.FadeAway) && State != LegacyNativeMobStatus.Respawning)
		{
			DateTime now = DateTime.Now;
			if (now > FadeAway.AddSeconds(3.0))
			{
				using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
				{
					LegacyNativePacket stream = recycledPacket.GetStream();
					LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
					{
						ObjId = UID,
						Type = 135
					};
					Send(stream.ActionCreate(pQuery));
				}
				State = LegacyNativeMobStatus.Respawning;
				map.View!.MoveTo((ILegacyNativeMapObj)this, (int)RespawnX, (int)RespawnY);
				X = RespawnX;
				Y = RespawnY;
				Target = null;
				return true;
			}
		}
		return false;
	}
}
