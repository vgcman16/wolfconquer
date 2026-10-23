using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Flowers239..251,293..300; real owner/method family.
internal partial class LegacyNativeFlowersRuntime
{
	public uint FreeFlowers;

	public int Day;

	public Flower RedRoses;

	public Flower Lilies;

	public Flower Orchids;

	public Flower Tulips;

	public static ConcurrentDictionary<uint, LegacyNativeFlowersRuntime> ClientPoll = new ConcurrentDictionary<uint, LegacyNativeFlowersRuntime>();

	public LegacyNativeFlowersRuntime(uint UID, string Name)
	{
		FreeFlowers = 1u;
		RedRoses = new Flower(LegacyNativeMsgFlower.FlowersType.Rouse, UID, Name);
		Lilies = new Flower(LegacyNativeMsgFlower.FlowersType.Lilies, UID, Name);
		Orchids = new Flower(LegacyNativeMsgFlower.FlowersType.Orchids, UID, Name);
		Tulips = new Flower(LegacyNativeMsgFlower.FlowersType.Tulips, UID, Name);
	}
}
