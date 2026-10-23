using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeConfiscatorTable
{
	public ConcurrentDictionary<uint, LegacyNativeConfiscatorRuntime> PollContainers = new ConcurrentDictionary<uint, LegacyNativeConfiscatorRuntime>();

	public void QueueObj(uint UID, LegacyNativeConfiscatorRuntime conainter)
	{
		if (!PollContainers.ContainsKey(UID))
		{
			PollContainers.TryAdd(UID, conainter);
		}
	}

	private int GetRedeemItemsCount()
	{
		return PollContainers.Values.Sum((LegacyNativeConfiscatorRuntime p) => p.RedeemContainer.Count);
	}

	private int GetClaimItemsCount()
	{
		return PollContainers.Values.Sum((LegacyNativeConfiscatorRuntime p) => p.ClaimContainer.Count);
	}

	internal void DeleteAllOf(uint UID)
	{
		if (!PollContainers.TryRemove(UID, out var _))
		{
			return;
		}
		foreach (LegacyNativeConfiscatorRuntime value4 in PollContainers.Values)
		{
			foreach (LegacyNativeMsgDetainedItem value5 in value4.RedeemContainer.Values)
			{
				if (value5.GainerUID == UID || value5.OwnerUID == UID)
				{
					value4.RedeemContainer.TryRemove(value5.UID, out var _);
				}
			}
			foreach (LegacyNativeMsgDetainedItem value6 in value4.ClaimContainer.Values)
			{
				if (value6.GainerUID == UID || value6.OwnerUID == UID)
				{
					value4.RedeemContainer.TryRemove(value6.UID, out var _);
				}
			}
		}
	}

}
