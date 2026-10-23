using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeServerMapPopulation
{
	public static void LoadPortals(uint id = 0u)
	{
		if (File.Exists(LegacyNativeServerConfig.DbLocation + "portals.ini"))
		{
			using StreamReader streamReader = File.OpenText(LegacyNativeServerConfig.DbLocation + "portals.ini");
			ushort num = 0;
			while (true)
			{
				string? text = streamReader.ReadLine();
				if (text == null)
				{
					break;
				}
				ushort num2 = ushort.Parse(text.Split('[')[1].ToString().Split(']')[0]);
				ushort num3 = ushort.Parse(streamReader.ReadLine()!.Split('=')[1]);
				for (ushort num4 = 0; num4 < num3; num4++)
				{
					LegacyNativePortal portal = new LegacyNativePortal();
					string[] array = streamReader.ReadLine()!.Split('=')[1].Split(' ');
					portal.MapID = ushort.Parse(array[0]);
					portal.X = ushort.Parse(array[1]);
					portal.Y = ushort.Parse(array[2]);
					string[] array2 = streamReader.ReadLine()!.Split('=')[1].Split(' ');
					portal.Destiantion_MapID = ushort.Parse(array2[0]);
					portal.Destiantion_X = ushort.Parse(array2[1]);
					portal.Destiantion_Y = ushort.Parse(array2[2]);
					if (id == 0 || id == num2)
					{
						if (LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey(portal.MapID))
						{
							LegacyNativePoolMaps.ServerMaps![portal.MapID].Portals.Add(portal);
						}
						num++;
					}
				}
			}
		}
		GC.Collect();
	}
}
