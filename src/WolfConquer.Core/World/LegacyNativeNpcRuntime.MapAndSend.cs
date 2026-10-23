using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native GMap/Send completes real NPC interface adoption against full lazy lookup.
internal partial class LegacyNativeNpcRuntime : ILegacyNativeMapObj
{
	public LegacyNativeGameMap GMap
	{
		get
		{
			if (_gmap == null)
			{
				_gmap = LegacyNativePoolMaps.ServerMaps![Map];
			}
			return _gmap!;
		}
	}

	public void Send(LegacyNativePacket stream)
	{
		foreach (ILegacyNativeMapObj item in GMap.View!.Roles(LegacyNativeMapObjectType.Player, X, Y))
		{
			if (CanSee(item))
			{
				UnKnow = UID;
				(item as LegacyNativePlayerRuntime)!.Send(stream.NpcCreate(this, 0));
			}
		}
	}
}
