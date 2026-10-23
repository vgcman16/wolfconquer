using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeServerMapPopulation
{
	public static void LoadMapMonsters(string file)
	{
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "MonsterSpawns\\" + file))
		{
			return;
		}
		using StreamReader streamReader = File.OpenText(LegacyNativeServerConfig.DbLocation + "MonsterSpawns\\" + file);
		while (true)
		{
			string? text = streamReader.ReadLine();
			if (text == null || !(text != ""))
			{
				break;
			}
			try
			{
				string[] array = text.Split(',');
				uint num = uint.Parse(array[1]);
				string text2 = array[2];
				if (text2.Contains("Titan"))
				{
					continue;
				}
				if (text2 == "WhiteTiger")
				{
				}
				ushort num2 = ushort.Parse(array[3]);
				ushort num3 = ushort.Parse(array[4]);
				uint num4 = uint.Parse(array[5]);
				LegacyNativeGameMap gameMap = LegacyNativePoolMaps.ServerMaps![num4];
				if (gameMap == null)
				{
					Console.WriteLine("LoadMapMonsters MapID:" + num4);
					break;
				}
				if (gameMap.MonstersColletion == null)
				{
					gameMap.MonstersColletion = new LegacyNativeMobCollection(gameMap.ID);
				}
				else if (gameMap.MonstersColletion.DMap == null)
				{
					gameMap.MonstersColletion.DMap = gameMap;
				}
				foreach (LegacyNativeMonsterFamily value in LegacyNativePoolMonsters.MonsterFamilies.Values)
				{
					if (value.Name == text2)
					{
						LegacyNativeMonsterFamily monsterFamily = value.Copy();
						monsterFamily.SpawnX = num2;
						monsterFamily.SpawnY = num3;
						monsterFamily.MaxSpawnX = (ushort)(num2 + 1);
						monsterFamily.MaxSpawnY = (ushort)(num3 + 1);
						monsterFamily.MapID = gameMap.ID;
						monsterFamily.SpawnCount = 1;
						LegacyNativeMonsterRole? monsterRole = gameMap.MonstersColletion.Add(monsterFamily, RemoveOnDead: false, 0u, num4 != 10166);
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				break;
			}
		}
	}
}
