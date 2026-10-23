namespace WolfConquer.Core.World;

// Whole native birth and map-binding family. MonsterRole spawning remains held,
// not acknowledged through a fake monster or a synthetic Add callback.
internal partial class LegacyNativeMobCollection
{
	public const byte Multiple = 3;

	public string LocationSpawn = "";

	public object SyncRoot = new object();

	public static LegacyNativeCounter GenerateUid = new LegacyNativeCounter(400000u);

	public LegacyNativeGameMap DMap = null;

	private uint DmapID = 0u;

	public LegacyNativeMobCollection(uint Map)
	{
		DmapID = Map;
		if (LegacyNativePoolMaps.ServerMaps != null && LegacyNativePoolMaps.ServerMaps.TryGetValue(Map, out DMap))
		{
			DMap.MonstersColletion = this;
		}
	}

	public bool ReadMap()
	{
		if (LegacyNativePoolMaps.ServerMaps != null && LegacyNativePoolMaps.ServerMaps.TryGetValue(DmapID, out DMap))
		{
			DMap.MonstersColletion = this;
		}
		return DMap != null;
	}
}
