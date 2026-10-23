using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Flowers253..291,302..305; real owner/method family.
internal partial class LegacyNativeFlowersRuntime
{
	public uint CreateFlowerIcon(Flower flow, bool Today = false, byte Rank = 0)
	{
		uint num = (Today ? 30000001u : 30000002u);
		if (flow == null)
		{
			return num + GetRank(Rank);
		}
		if (flow.Rank == 0 || flow.Rank > 100)
		{
			return 0u;
		}
		return (uint)((int)num + 100 * (byte)flow.Type + GetRank(flow.Rank));
	}

	public uint CreateBoyIcon(Flower flow)
	{
		return (uint)(30000402 + 100 * (byte)flow.Type + GetRank(flow.Rank));
	}

	public ushort GetRank(int rank)
	{
		if (rank == 1)
		{
			return 0;
		}
		if (rank == 2)
		{
			return 10000;
		}
		if (rank == 3)
		{
			return 20000;
		}
		if (rank > 3)
		{
			return 30000;
		}
		return 0;
	}

	public uint AllFlowersToday()
	{
		return (uint)this.Sum((Flower x) => x.Amount2day);
	}
}
