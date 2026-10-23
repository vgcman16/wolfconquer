using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public void AddMapMonster(LegacyNativePacket stream, uint ID, ushort x, ushort y, ushort max_x, ushort max_y, byte count, uint DinamicID = 0u, bool RemoveOnDead = false, LegacyNativeMsgItemPacket.EffectMonsters m_effect = LegacyNativeMsgItemPacket.EffectMonsters.None, string streffect = "")
	{
		if (MonstersColletion == null)
		{
			MonstersColletion = new LegacyNativeMobCollection(ID);
		}
		if (!MonstersColletion.ReadMap() || !LegacyNativePoolMonsters.MonsterFamilies.TryGetValue(ID, out var value))
		{
			return;
		}
		LegacyNativeMonsterFamily monsterFamily = value.Copy();
		monsterFamily.SpawnX = x;
		monsterFamily.SpawnY = y;
		monsterFamily.MaxSpawnX = (ushort)(x + max_x);
		monsterFamily.MaxSpawnY = (ushort)(y + max_y);
		monsterFamily.MapID = ID;
		monsterFamily.SpawnCount = count;
		LegacyNativeMonsterRole? monsterRole = MonstersColletion.Add(monsterFamily, RemoveOnDead, DinamicID, justone: true);
		if (monsterRole == null)
		{
			LegacyNativeConsole.WriteLine("[Error] Add Map Monster (" + ID + ")", ConsoleColor.DarkRed);
			return;
		}
		LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
		{
			ObjId = monsterRole.UID,
			Type = 134,
			PositionX = monsterRole.X,
			PositionY = monsterRole.Y
		};
		monsterRole.Send(stream.ActionCreate(pQuery));
		monsterRole.Send(monsterRole.GetArray(stream, view: false, st: false));
		if (streffect != null)
		{
			monsterRole.SendString(stream, LegacyNativeMsgStringPacket.StringID.Effect, streffect);
		}
		if (m_effect != LegacyNativeMsgItemPacket.EffectMonsters.None && monsterRole != null)
		{
			LegacyNativeMsgItemPacket msgItemPacket = LegacyNativeMsgItemPacket.Create();
			msgItemPacket.m_UID = (uint)m_effect;
			msgItemPacket.m_X = monsterRole.X;
			msgItemPacket.m_Y = monsterRole.Y;
			msgItemPacket.DropType = LegacyNativeMsgDropID.Earth;
			monsterRole.Send(stream.ItemPacketCreate(msgItemPacket));
			monsterRole.SendString(stream, LegacyNativeMsgStringPacket.StringID.Effect, "glebesword");
		}
		if (monsterRole.HitPoints > 65535)
		{
			LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, monsterRole.UID);
			stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.MaxHitpoints, monsterRole.Family.MaxHealth);
			stream = msgUpdate.GetArray(stream);
			monsterRole.Send(stream);
			stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, monsterRole.HitPoints);
			stream = msgUpdate.GetArray(stream);
			monsterRole.Send(stream);
		}
	}
}
