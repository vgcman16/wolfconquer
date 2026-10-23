using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeMsgExtremePk
{
	public void SendMapPacket(LegacyNativePacket stream)
	{
		LegacyNativeGameClientRuntime[] array = MapPlayers();
		foreach (LegacyNativeGameClientRuntime gameClient in array)
		{
			gameClient.Send(stream);
		}
	}

	public bool InTournament(LegacyNativeGameClientRuntime user)
	{
		if (Map == null)
		{
			return false;
		}
		return user.Player.Map == Map.ID && user.Player.DynamicID == DinamicID;
	}

	public LegacyNativeGameClientRuntime[] MapPlayers()
	{
		return Map!.Values.Where((LegacyNativeGameClientRuntime p) => InTournament(p)).ToArray();
	}
}
