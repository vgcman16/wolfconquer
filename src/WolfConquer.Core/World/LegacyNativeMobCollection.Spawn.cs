using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMobCollection
{
	public LegacyNativeMonsterRole? Add(LegacyNativeMonsterFamily Famili, bool RemoveOnDead = false, uint dinamicid = 0u, bool justone = false)
	{
		if (DMap == null)
		{
			ReadMap();
		}
		if (Famili.ID == 8303)
		{
		}
		LegacyNativeMonsterRole? result = null;
		int num = ((Famili.Boss > 0) ? 1 : (Math.Max(1, (int)Famili.SpawnCount) * 3));
		if (justone)
		{
			num = 1;
		}
		if ((DmapID == 1011 || DmapID == 3071 || DmapID == 1770 || DmapID == 1771 || DmapID == 1772 || DmapID == 1773 || DmapID == 1774 || DmapID == 1775 || DmapID == 1777 || DmapID == 1782 || DmapID == 1785 || DmapID == 1786 || DmapID == 1787 || DmapID == 1794) && num > 1 && Famili.SpawnCount > 1)
		{
			num = (Famili.MaxSpawnX - Famili.SpawnX) / Famili.SpawnCount * ((Famili.MaxSpawnY - Famili.SpawnY) / Famili.SpawnCount);
		}
		for (int i = 0; i < num; i++)
		{
			LegacyNativeMonsterRole monsterRole = new LegacyNativeMonsterRole(Famili.Copy(), GenerateUid.Next, LocationSpawn, DMap);
			monsterRole.RemoveOnDead = RemoveOnDead;
			ushort X = 0;
			ushort Y = 0;
			TryObtainSpawnXY(Famili, out X, out Y);
			if (DMap!.ValidLocation(X, Y) && !DMap.MonsterOnTile(X, Y))
			{
				DMap.SetMonsterOnTile(X, Y, Value: true);
				monsterRole.X = X;
				monsterRole.Y = Y;
				monsterRole.RespawnX = X;
				monsterRole.RespawnY = Y;
				monsterRole.Map = DMap.ID;
				monsterRole.DynamicID = dinamicid;
				result = monsterRole;
				DMap.View!.EnterMap(monsterRole);
			}
		}
		return result;
	}

	public void TryObtainSpawnXY(LegacyNativeMonsterFamily Monster, out ushort X, out ushort Y)
	{
		X = (ushort)LegacyNativePoolRandom.GetRandom.Next(Monster.SpawnX, Monster.MaxSpawnX);
		Y = (ushort)LegacyNativePoolRandom.GetRandom.Next(Monster.SpawnY, Monster.MaxSpawnY);
		byte b = 0;
		while (b < 10 && DMap != null && (!DMap!.ValidLocation(X, Y) || DMap.MonsterOnTile(X, Y)))
		{
			X = (ushort)LegacyNativePoolRandom.GetRandom.Next(Monster.SpawnX, Monster.MaxSpawnX);
			Y = (ushort)LegacyNativePoolRandom.GetRandom.Next(Monster.SpawnY, Monster.MaxSpawnY);
			b++;
		}
	}
}
