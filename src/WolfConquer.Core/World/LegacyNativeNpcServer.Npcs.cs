using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNpcServer
{
	public static void LoadNpcs(uint id = 0u)
	{
		uint num = 0u;
		if (File.Exists(LegacyNativeServerConfig.DbLocation + "npcs.txt"))
		{
			using StreamReader streamReader = File.OpenText(LegacyNativeServerConfig.DbLocation + "npcs.txt");
			while (true)
			{
				string? text = streamReader.ReadLine();
				if (text == null || !(text != ""))
				{
					break;
				}
				string[] array = text.Split(',');
				if (id != 0 && id != ushort.Parse(array[3]))
				{
					continue;
				}
				LegacyNativeNpcRuntime npc = LegacyNativeNpcRuntime.Create();
				npc.UID = uint.Parse(array[0]);
				npc.NpcType = (LegacyNativeNpcType)byte.Parse(array[1]);
				npc.Mesh = ushort.Parse(array[2]);
				npc.Map = ushort.Parse(array[3]);
				npc.X = ushort.Parse(array[4]);
				npc.Y = ushort.Parse(array[5]);
				if (npc.Mesh != 42580)
				{
					if (npc.UID == 16851 || npc.UID == 168052)
					{
						npc.AllowDynamic = true;
					}
					else if (array.Length > 6)
					{
						npc.AllowDynamic = ushort.Parse(array[6]) == 1;
					}
					if (LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey(npc.Map))
					{
						num++;
						LegacyNativePoolMaps.ServerMaps![npc.Map].AddNpc(npc);
					}
				}
			}
		}
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "furnitures.txt"))
		{
			return;
		}
		using StreamReader streamReader2 = File.OpenText(LegacyNativeServerConfig.DbLocation + "furnitures.txt");
		while (true)
		{
			string? text2 = streamReader2.ReadLine();
			if (text2 == null)
			{
				break;
			}
			string[] array2 = text2.Split(',');
			if (id == 0 || id == ushort.Parse(array2[3]))
			{
				LegacyNativeNpcRuntime npc2 = LegacyNativeNpcRuntime.Create();
				npc2.UID = uint.Parse(array2[0]);
				npc2.NpcType = (LegacyNativeNpcType)byte.Parse(array2[1]);
				npc2.Mesh = ushort.Parse(array2[2]);
				npc2.Map = ushort.Parse(array2[3]);
				npc2.X = ushort.Parse(array2[4]);
				npc2.Y = ushort.Parse(array2[5]);
				Furniture furniture = new Furniture();
				furniture.Name = array2[6];
				furniture.ItemID = uint.Parse(array2[7]);
				furniture.UID = npc2.UID;
				furniture.Mesh = npc2.Mesh;
				furniture.MoneyCost = int.Parse(array2[8]);
				if (!FurnitureInformations.ContainsKey(npc2.UID))
				{
					FurnitureInformations.Add(npc2.UID, furniture);
				}
				if (LegacyNativePoolMaps.ServerMaps!.Base.ContainsKey(npc2.Map))
				{
					LegacyNativePoolMaps.ServerMaps![npc2.Map].AddNpc(npc2);
				}
			}
		}
	}
}
