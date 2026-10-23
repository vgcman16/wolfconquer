using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeConfiscatorRuntime
{
	public static LegacyNativeCounter CounterUID = new LegacyNativeCounter(1u);

	public ConcurrentDictionary<uint, LegacyNativeMsgDetainedItem> RedeemContainer;

	public ConcurrentDictionary<uint, LegacyNativeMsgDetainedItem> ClaimContainer;

	public LegacyNativeConfiscatorRuntime()
	{
		RedeemContainer = new ConcurrentDictionary<uint, LegacyNativeMsgDetainedItem>();
		ClaimContainer = new ConcurrentDictionary<uint, LegacyNativeMsgDetainedItem>();
	}

	public bool IsEmptyReadem()
	{
		return RedeemContainer.IsEmpty;
	}

	public bool IsEmptyClaim()
	{
		return ClaimContainer.IsEmpty;
	}


}
