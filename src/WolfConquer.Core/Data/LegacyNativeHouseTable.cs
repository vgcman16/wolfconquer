using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native MrRayzo.Database/HouseTable.cs; no host birth, startup, save caller or SQL route installed.
internal class LegacyNativeHouseTable
{
	internal static int CountFurnitures(byte Level)
	{
		return Level switch
		{
			2 => 8,
			3 => 9,
			4 => 10,
			5 => 12,
			6 => 20,
			_ => 0,
		};
	}

	internal static bool InHouse(uint MapID)
	{
		return MapID == 1098 || MapID == 1099 || MapID == 2080 || MapID == 601 || MapID == 3024 || MapID == 3995;
	}

	internal static ushort GetHouseId(byte level)
	{
		return level switch
		{
			1 => 1098,
			2 => 1099,
			3 => 2080,
			4 => 601,
			5 => 3024,
			6 => 3995,
			_ => 0,
		};
	}
}
