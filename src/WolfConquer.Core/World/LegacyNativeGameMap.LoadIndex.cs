using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public static void LoadMaps()
	{
		using BinaryReader binaryReader = new BinaryReader(new FileStream(Path.Combine(LegacyNativeServerConfig.CO2Folder, "ini/gamemap.dat"), FileMode.Open));
		int num = binaryReader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			int key = binaryReader.ReadInt32();
			string text = Encoding.ASCII.GetString(binaryReader.ReadBytes(binaryReader.ReadInt32()));
			int num2 = binaryReader.ReadInt32();
			MapContents[key] = text.Replace(".7z", ".dmap");
		}
	}
}
