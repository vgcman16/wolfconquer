using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClanRuntime
{
    // Whole source Clan40..70. Source has NO explicit constructor; these are genuine implicit initializers.
	public static uint[] UpdateLevelAmount = new uint[4] { 1000000u, 2000000u, 3500000u, 7500000u };

	public static uint[] UpdateBPAmount = new uint[4] { 2500000u, 12000000u, 35000000u, 120000000u };

	public static ConcurrentDictionary<uint, string?> ChangeNameRecords = new ConcurrentDictionary<uint, string?>();

	public static LegacyNativeCounter CounterClansID = new LegacyNativeCounter(10u);

	public static ConcurrentDictionary<uint, LegacyNativeClanRuntime> Clans = new ConcurrentDictionary<uint, LegacyNativeClanRuntime>();

	public ConcurrentDictionary<uint, LegacyNativeClanRuntime> Ally = new ConcurrentDictionary<uint, LegacyNativeClanRuntime>();

	public ConcurrentDictionary<uint, LegacyNativeClanRuntime> Enemy = new ConcurrentDictionary<uint, LegacyNativeClanRuntime>();

	public ConcurrentDictionary<uint, LegacyNativeClanMember> Members = new ConcurrentDictionary<uint, LegacyNativeClanMember>();

	public uint RequestAlly = 0u;

	public string? Name = "";

	public string? LeaderName = "";

	public uint ID = 0u;

	public byte Level = 1;

	public uint Donation = 0u;

	public string? ClanBuletin = "None";

	public byte BP = 0;


}
