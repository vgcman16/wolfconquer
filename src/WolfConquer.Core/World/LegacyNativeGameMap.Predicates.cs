using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public static bool IsGate(uint UID)
	{
		return UID == 516076 || UID == 516077 || UID == 516074 || UID == 516075 || UID == 516078 || UID == 516079 || UID == 516080;
	}

	public static bool IsFrozengrotoMaps(uint Map)
	{
		return Map == 1762 || Map == 1927 || Map == 1999 || Map == 2054 || Map == 2055 || Map == 2056;
	}

	public uint GenerateDynamicID()
	{
		return DinamicIDS.Next;
	}
}
