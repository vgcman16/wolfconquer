using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native lazy loader and all population calls. Requires genuine incoming monster graph.
internal partial class LegacyNativeGameMap
{
	public static bool CheckMap(uint ID)
	{
		if (!LegacyNativePoolMaps.ServerMaps!.ContainsKey(ID))
		{
			try
			{
				if (MapContents.ContainsKey((int)ID))
				{
					return LoadMap((int)ID, MapContents[(int)ID]);
				}
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				LegacyNativeConsole.WriteLine("MapID = " + ID + " not exist.", ConsoleColor.DarkRed);
				return false;
			}
		}
		return true;
	}

public static void EnterMap(int id)
	{
		try
		{
			if (LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey((uint)id) || id == 0)
			{
				return;
			}
			uint num = (uint)id;
			if (num >= 3830 && num <= 3834)
			{
				num = 1780u;
			}
			else if (num >= 3826 && num <= 3829)
			{
				num = 3825u;
			}
			else if (num == 7357)
			{
				num = 6000u;
			}
			else if (num == 5040)
			{
				num = 5000u;
			}
			else if (num == 5342)
			{
				num = 700u;
			}
			else if (num == 5339)
			{
				num = 3851u;
			}
			else if (num == 2351)
			{
				num = 1351u;
			}
			else if (num == 2352)
			{
				num = 1351u;
			}
			else if (num == 2353)
			{
				num = 1351u;
			}
			else if (num == 6011)
			{
				num = 1021u;
			}
			else if (num == 9988)
			{
				num = 8709u;
			}
			else if (num == 8437)
			{
				num = 1801u;
			}
			else if (num == 3833)
			{
				num = 3825u;
			}
			else if (num == 1818)
			{
				num = 1765u;
			}
			else if (num == 1052)
			{
				num = 1082u;
			}
			else if (num == 6072)
			{
				num = 1004u;
			}
			else if (num == 1782)
			{
				num = 1004u;
			}
			else if (num == 1783)
			{
				num = 1004u;
			}
			else if (num == 1784)
			{
				num = 601u;
			}
			else if (num == 1794)
			{
				num = 1028u;
			}
			else if (num == 5599)
			{
				num = 1016u;
			}
			else if (num == 1792)
			{
				num = 1014u;
			}
			else if (num == 1791)
			{
				num = 1765u;
			}
			else if (num == 6784)
			{
				num = 1505u;
			}
			else if (num >= 44455 && num <= 44457)
			{
				num = 10088u;
			}
			else if (num >= 44460 && num <= 44463)
			{
				num = 10090u;
			}
			else
			{
				switch (num)
				{
				case 6546u:
					num = 3872u;
					break;
				case 6521u:
					num = 2071u;
					break;
				case 8009u:
					num = 2071u;
					break;
				case 6891u:
					num = 2071u;
					break;
				case 6525u:
					num = 1507u;
					break;
				case 8521u:
					num = 1507u;
					break;
				case 6729u:
					num = 3825u;
					break;
				}
			}
			if (MapContents.ContainsKey((int)num) && LoadMap(id, MapContents[(int)num], (id != num) ? num : 0u))
			{
				LegacyNativeServerMapPopulation.LoadMapName((uint)id);
				LegacyNativeNpcServer.LoadNpcs((uint)id);
				LegacyNativeNpcServer.LoadSobNpcs((uint)id);
				LegacyNativeServerMapPopulation.LoadPortals((uint)id);
				LegacyNativeNpcServer.LoadServerTraps((uint)id);
				switch (id)
				{
				case 1002:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1002.txt");
					break;
				case 1020:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1020.txt");
					break;
				case 1011:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1011.txt");
					break;
				case 1768:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1768.txt");
					break;
				case 1000:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1000.txt");
					break;
				case 1001:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1001.txt");
					break;
				case 1779:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1779.txt");
					break;
				case 1780:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1780.txt");
					break;
				case 3935:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters3935.txt");
					break;
				case 3998:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters3998.txt");
					break;
				case 1043:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1043.txt");
					break;
				case 1044:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1044.txt");
					break;
				case 1045:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1045.txt");
					break;
				case 1046:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1046.txt");
					break;
				case 1047:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1047.txt");
					break;
				case 1048:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1048.txt");
					break;
				case 8822:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters8822.txt");
					break;
				case 1015:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1015.txt");
					break;
				case 1016:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters1016.txt");
					break;
				case 3845:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters3845.txt");
					break;
				case 10250:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10250.txt");
					break;
				case 10166:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10166.txt");
					break;
				case 10089:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10089.txt");
					break;
				case 11127:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters11127.txt");
					break;
				case 10137:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10137.txt");
					break;
				case 3071:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters3071.txt");
					break;
				case 10341:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10341.txt");
					break;
				case 10342:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10342.txt");
					break;
				case 10323:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10323.txt");
					break;
				case 10324:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10324.txt");
					break;
				case 10322:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10322.txt");
					break;
				case 10343:
					LegacyNativeServerMapPopulation.LoadMapMonsters("Monsters10343.txt");
					break;
				}
			}
			GC.Collect();
		}
		catch (Exception e)
		{
			LegacyNativeConsole.WriteException(e);
		}
	}
}
