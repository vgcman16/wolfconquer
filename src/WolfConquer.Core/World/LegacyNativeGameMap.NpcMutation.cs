using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public void AddNpc(LegacyNativeNpcRuntime npc)
	{
		if (!View!.MapContain(LegacyNativeMapObjectType.Npc, npc.UID))
		{
			View!.EnterMap((ILegacyNativeMapObj)npc);
			SetFlagNpc(npc.X, npc.Y);
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			npc.Send(stream);
			return;
		}
		using (LegacyNativeRecycledPacket recycledPacket2 = new LegacyNativeRecycledPacket())
		{
			LegacyNativePacket stream2 = recycledPacket2.GetStream();
			RemoveNpc(npc, stream2);
		}
		if (!View!.MapContain(LegacyNativeMapObjectType.Npc, npc.UID))
		{
			View!.EnterMap((ILegacyNativeMapObj)npc);
			SetFlagNpc(npc.X, npc.Y);
			using LegacyNativeRecycledPacket recycledPacket3 = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream3 = recycledPacket3.GetStream();
			npc.Send(stream3);
		}
	}

	public void RemoveNpc(LegacyNativeNpcRuntime npc, LegacyNativePacket stream)
	{
		if (!View!.MapContain(LegacyNativeMapObjectType.Npc, npc.UID))
		{
			return;
		}
		View!.LeaveMap((ILegacyNativeMapObj)npc);
		RemoveFlagNpc(npc.X, npc.Y);
		LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
		{
			ObjId = npc.UID,
			Type = 135
		};
		foreach (ILegacyNativeMapObj item in View!.Roles(LegacyNativeMapObjectType.Player, npc.X, npc.Y))
		{
			if (LegacyNativeRoleCore.GetDistance(item.X, item.Y, npc.X, npc.Y) <= 19)
			{
				item.Send(stream.ActionCreate(pQuery));
			}
		}
	}

	public void RemoveNpc(LegacyNativeSobNpcRuntime npc, LegacyNativePacket stream)
	{
		if (!View!.MapContain(LegacyNativeMapObjectType.SobNpc, npc.UID))
		{
			return;
		}
		View!.LeaveMap((ILegacyNativeMapObj)npc);
		RemoveFlagNpc(npc.X, npc.Y);
		LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
		{
			ObjId = npc.UID,
			Type = 135
		};
		foreach (ILegacyNativeMapObj item in View!.Roles(LegacyNativeMapObjectType.Player, npc.X, npc.Y))
		{
			if (LegacyNativeRoleCore.GetDistance(item.X, item.Y, npc.X, npc.Y) <= 19)
			{
				item.Send(stream.ActionCreate(pQuery));
			}
		}
	}
}
