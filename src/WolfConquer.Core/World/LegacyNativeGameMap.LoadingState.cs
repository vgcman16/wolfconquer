using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public static object EnterObj = new object();


	public static sbyte[] XDir = new sbyte[32]
	{
		-1, -2, -2, -1, 1, 2, 2, 1, 0, -2,
		-2, -2, 0, 2, 2, 2, -1, -2, -2, -1,
		1, 2, 2, 1, 0, -1, -1, -1, 0, 1,
		1, 1
	};

	public static sbyte[] YDir = new sbyte[32]
	{
		2, 1, -1, -2, -2, -1, 1, 2, 2, 2,
		0, -2, -2, -2, 0, 2, 2, 1, -1, -2,
		-2, -1, 1, 2, 1, 1, 0, -1, -1, -1,
		0, 1
	};

	private static List<ushort> UsingMaps = new List<ushort>
	{
		601, 700, 1000, 1001, 1002, 1004, 1005, 1006, 1008, 1010,
		1011, 1013, 1015, 1020, 1036, 1511, 1038, 2068, 6001, 1098,
		1099, 2080, 601, 3024, 1351, 1352, 1353, 1354, 1762, 1927,
		1999, 2054, 2055, 2056, 1858, 3846, 1700, 3851, 3055, 3056,
		3846, 1039, 6000, 3825, 2057, 1764, 2068
	};

	public List<LegacyNativePortal> Portals = new List<LegacyNativePortal>();

	public string Name = "";

	public uint BaseID = 0u;


	public LegacyNativeMobCollection MonstersColletion;


	public LegacyNativeNpcRuntime Magnolia = null;

	public ConcurrentDictionary<uint, LegacyNativeNpcRuntime> soldierRemains = new ConcurrentDictionary<uint, LegacyNativeNpcRuntime>();

	public static LegacyNativeCounter DinamicIDS = new LegacyNativeCounter(10000001u);

	public ushort Reborn_Map = 0;

	public ushort Reborn_X = 0;

	public ushort Reborn_Y = 0;

	public static Dictionary<int, string> MapContents = new Dictionary<int, string>();

	public uint MapColor = 0u;

	public int[,] FloorType;

	public int[,] Altitude;

	private bool Update = false;

	private LegacyNativeGameClientRuntime[] Users = new LegacyNativeGameClientRuntime[0];


	public object SyncRoot = new object();



	public uint TypeStatus { get; set; }
}
