using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeMsgLastManStand
{
	public bool InTournament(LegacyNativeGameClientRuntime user)
	{
		if (Map == null)
		{
			return false;
		}
		return user.Player.Map == Map.ID && user.Player.DynamicID == DinamicMap;
	}

	public LegacyNativeGameClientRuntime[] MapPlayers()
	{
		return Map!.Values.Where((LegacyNativeGameClientRuntime p) => p.Player.DynamicID == DinamicMap && p.Player.Map == Map!.ID).ToArray();
	}
}
