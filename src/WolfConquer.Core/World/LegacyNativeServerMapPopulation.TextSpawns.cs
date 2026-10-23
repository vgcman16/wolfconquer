using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Complete native Server method family; no startup or worker is registered.
internal static partial class LegacyNativeServerMapPopulation
{
	public static void LoadMyMonsters(uint id)
	{
		try
		{
			LegacyNativeMobCollection mobCollection = new LegacyNativeMobCollection(id);
			if (mobCollection.ReadMap())
			{
				string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "MonsterSpawns\\myMonsters.txt");
				string[] array2 = array;
				foreach (string text in array2)
				{
					string[] array3 = text.Split(' ');
					ushort num = (ushort)long.Parse(array3[1]);
					if (id == num)
					{
						uint key = (uint)long.Parse(array3[9]);
						ushort num2 = (ushort)long.Parse(array3[6]);
						ushort num3 = (ushort)long.Parse(array3[2]);
						ushort num4 = (ushort)long.Parse(array3[3]);
						ushort num5 = (ushort)long.Parse(array3[4]);
						ushort num6 = (ushort)long.Parse(array3[5]);
						int num7 = (int)long.Parse(array3[8]);
						uint rest_secs = (uint)long.Parse(array3[7]);
						if (LegacyNativePoolMonsters.MonsterFamilies.TryGetValue(key, out var value) && !LegacyNativeMonsterRole.SpecialMonsters.Contains(value.ID))
						{
							LegacyNativeMonsterFamily monsterFamily = value.Copy();
							monsterFamily.SpawnX = num3;
							monsterFamily.SpawnY = num4;
							monsterFamily.MaxSpawnX = (ushort)(num3 + num5);
							monsterFamily.MaxSpawnY = (ushort)(num4 + num6);
							monsterFamily.MapID = num;
							monsterFamily.SpawnCount = (byte)num7;
							monsterFamily.rest_secs = (int)rest_secs;
							mobCollection.Add(monsterFamily);
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
