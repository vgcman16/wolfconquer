using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Complete native Server method family; no startup or worker is registered.
internal static partial class LegacyNativeServerMapPopulation
{
	public static void LoadMonsters(uint id = 0u)
	{
		try
		{
			LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("");
			if (Directory.Exists(LegacyNativeServerConfig.DbLocation + "\\MonsterSpawns\\" + id + "\\"))
			{
				string[] directories = Directory.GetDirectories(LegacyNativeServerConfig.DbLocation + "\\MonsterSpawns\\" + id + "\\");
				foreach (string path in directories)
				{
					LegacyNativeMobCollection mobCollection = new LegacyNativeMobCollection(id);
					if (!mobCollection.ReadMap())
					{
						continue;
					}
					string[] directories2 = Directory.GetDirectories(path);
					foreach (string path2 in directories2)
					{
						string[] files = Directory.GetFiles(path2);
						for (int k = 0; k < files.Length; k++)
						{
							mobCollection.LocationSpawn = (iniFile.FileName = files[k]);
							uint key = iniFile.ReadUInt32("cq_generator", "npctype");
							if (LegacyNativePoolMonsters.MonsterFamilies.TryGetValue(key, out var value) && !LegacyNativeMonsterRole.SpecialMonsters.Contains(value.ID))
							{
								LegacyNativeMonsterFamily monsterFamily = value.Copy();
								monsterFamily.SpawnX = iniFile.ReadUInt16("cq_generator", "bound_x", 0);
								monsterFamily.SpawnY = iniFile.ReadUInt16("cq_generator", "bound_y", 0);
								monsterFamily.MaxSpawnX = (ushort)(monsterFamily.SpawnX + iniFile.ReadUInt16("cq_generator", "bound_cx", 0));
								monsterFamily.MaxSpawnY = (ushort)(monsterFamily.SpawnY + iniFile.ReadUInt16("cq_generator", "bound_cy", 0));
								monsterFamily.MapID = iniFile.ReadUInt32("cq_generator", "mapid");
								monsterFamily.SpawnCount = iniFile.ReadByte("cq_generator", "max_per_gen", 0);
								monsterFamily.rest_secs = iniFile.ReadInt32("cq_generator", "rest_secs", 0);
								if (monsterFamily.MapID == 1011 || monsterFamily.MapID == 3071 || monsterFamily.MapID == 1770 || monsterFamily.MapID == 1771 || monsterFamily.MapID == 1772 || monsterFamily.MapID == 1773 || monsterFamily.MapID == 1774 || monsterFamily.MapID == 1775 || monsterFamily.MapID == 1777 || monsterFamily.MapID == 1782 || monsterFamily.MapID == 1785 || monsterFamily.MapID == 1786 || monsterFamily.MapID == 1787 || monsterFamily.MapID == 1794)
								{
									monsterFamily.SpawnCount = iniFile.ReadByte("cq_generator", "maxnpc", 0);
								}
								mobCollection.Add(monsterFamily);
							}
						}
					}
				}
			}
			GC.Collect();
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
