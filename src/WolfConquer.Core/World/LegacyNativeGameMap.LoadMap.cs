using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public static bool LoadMap(int id, string mapFile, uint baseid = 0u)
	{
		try
		{
			lock (EnterObj)
			{
				LegacyNativeGameMap gameMap;
				using (BinaryReader binaryReader = new BinaryReader(new FileStream(Path.Combine(LegacyNativeServerConfig.CO2Folder, mapFile), FileMode.Open)))
				{
					binaryReader.ReadBytes(268);
					gameMap = new LegacyNativeGameMap(binaryReader.ReadInt32(), binaryReader.ReadInt32(), id);
					gameMap.MonstersColletion = new LegacyNativeMobCollection((uint)id);
					gameMap.View = new LegacyNativeMapView(gameMap.bounds.Width, gameMap.bounds.Height);
					gameMap.MonstersColletion = new LegacyNativeMobCollection((uint)id);
					gameMap.BaseID = baseid;
					if (id == 1038)
					{
						gameMap.FloorType = new int[gameMap.bounds.Width, gameMap.bounds.Height];
					}
					gameMap.Altitude = new int[gameMap.bounds.Width, gameMap.bounds.Height];
					for (int i = 0; i < gameMap.bounds.Height; i++)
					{
						for (int j = 0; j < gameMap.bounds.Width; j++)
						{
							gameMap.cells[j, i] = ((binaryReader.ReadInt16() == 0) ? LegacyNativeMapFlagType.Valid : LegacyNativeMapFlagType.None);
							if (id == 1038)
							{
								gameMap.FloorType[j, i] = binaryReader.ReadInt16();
							}
							else
							{
								binaryReader.ReadUInt16();
							}
							gameMap.Altitude[j, i] = binaryReader.ReadInt16();
						}
						binaryReader.ReadInt32();
					}
				}
				int num = ((baseid != 0) ? ((int)baseid) : id);
				if (File.Exists(LegacyNativeServerConfig.DbLocation + "maps\\" + num + ".ini"))
				{
					LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\maps\\" + num + ".ini");
					gameMap.TypeStatus = iniFile.ReadUInt32("info", "type");
					gameMap.Reborn_X = iniFile.ReadUInt16("info", "portal0_x", 0);
					gameMap.Reborn_Y = iniFile.ReadUInt16("info", "portal0_y", 0);
					gameMap.Reborn_Map = iniFile.ReadUInt16("info", "reborn_map", 0);
					gameMap.RecordSteedRace = iniFile.ReadUInt16("info", "race_record", 0);
					gameMap.MapColor = iniFile.ReadUInt32("info", "color");
				}
				LegacyNativePoolMaps.ServerMaps.Add((uint)id, gameMap);
				return true;
			}
		}
		catch (FileNotFoundException)
		{
			LegacyNativeConsole.WriteLine("\tMap not found: " + id + " - " + mapFile);
		}
		catch (Exception e)
		{
			LegacyNativeConsole.WriteException(e);
		}
		return false;
	}
}
