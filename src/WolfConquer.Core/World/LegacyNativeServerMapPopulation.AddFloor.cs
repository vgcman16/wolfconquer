using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Complete native Server method family; no startup or worker is registered.
internal static partial class LegacyNativeServerMapPopulation
{
	public static bool AddFloor(LegacyNativePacket stream, LegacyNativeGameMap map, uint ID, ushort x, ushort y, ushort spelllevel, LegacyNativeMagicType.Magic dbspell, LegacyNativeGameClientRuntime Owner, uint GuildID, uint OwnerUID, uint DinamicID = 0u, string Name = "", bool RemoveOnDead = true)
	{
		try
		{
			if (map.MonstersColletion == null)
			{
				map.MonstersColletion = new LegacyNativeMobCollection(map.ID);
			}
			if (map.MonstersColletion.ReadMap() && LegacyNativePoolMonsters.MonsterFamilies.TryGetValue(1u, out var value))
			{
				LegacyNativeMonsterFamily monsterFamily = value.Copy();
				monsterFamily.SpawnX = x;
				monsterFamily.SpawnY = y;
				monsterFamily.MaxSpawnX = (ushort)(x + 1);
				monsterFamily.MaxSpawnY = (ushort)(y + 1);
				monsterFamily.MapID = map.ID;
				monsterFamily.SpawnCount = 1;
				LegacyNativeMonsterRole? monsterRole = map.MonstersColletion.Add(monsterFamily, RemoveOnDead, DinamicID, justone: true);
				if (monsterRole == null)
				{
					return false;
				}
				monsterRole.Family.ID = ID;
				monsterRole.IsFloor = true;
				monsterRole.FloorStampTimer = DateTime.Now.AddSeconds(7.0);
				monsterRole.Family.Settings = LegacyNativeMonsterSettings.Lottus;
				monsterRole.FloorPacket = new LegacyNativeMsgItemPacket();
				monsterRole.FloorPacket.m_UID = monsterRole.UID;
				monsterRole.FloorPacket.m_ID = ID;
				monsterRole.FloorPacket.m_X = x;
				monsterRole.FloorPacket.m_Y = y;
				monsterRole.FloorPacket.MaxLife = 25;
				monsterRole.FloorPacket.Life = 25u;
				monsterRole.FloorPacket.DropType = LegacyNativeMsgDropID.Effect;
				monsterRole.FloorPacket.m_Color = 13;
				monsterRole.FloorPacket.ItemOwnerUID = OwnerUID;
				monsterRole.FloorPacket.GuildID = GuildID;
				monsterRole.FloorPacket.FlowerType = 2;
				monsterRole.FloorPacket.Timer = LegacyNativeRoleCore.TqTimer(monsterRole.FloorStampTimer);
				monsterRole.FloorPacket.Name = Name;
				monsterRole.DBSpell = dbspell;
				monsterRole.Family.MaxHealth = 25;
				monsterRole.HitPoints = 25u;
				monsterRole.OwnerFloor = Owner;
				monsterRole.SpellLevel = spelllevel;
				if (monsterRole == null)
				{
					Console.WriteLine("Eror monster spawn. Server.");
					return false;
				}
				map.View!.EnterMap((ILegacyNativeMapObj)monsterRole);
				monsterRole.Send(monsterRole.GetArray(stream, view: false, st: false));
				return true;
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.ToString());
		}
		return false;
	}
}
