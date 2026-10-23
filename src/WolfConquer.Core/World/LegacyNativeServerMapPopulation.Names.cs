using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeServerMapPopulation
{
	public static void LoadMapName(uint id)
	{
		if (File.Exists(LegacyNativeServerConfig.DbLocation + "GameMapEx.ini"))
		{
			LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("GameMapEx.ini");
			LegacyNativePoolMaps.ServerMaps![id].Name = iniFile.ReadString(id.ToString(), "Name", LegacyNativeServerConfig.ServerName);
		}
	}
}
