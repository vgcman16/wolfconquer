using System.Linq;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRankItems
{
    // Whole native physical RankPoll and CreateRanks; repeated creation still faults.
	public static Dictionary<uint, Rank> RankPoll = new Dictionary<uint, Rank>();

	public static void CreateRanks()
	{
		for (int i = 0; i < 11; i++)
		{
			RankPoll.Add((uint)(i + 1), new Rank());
		}
	}

}
