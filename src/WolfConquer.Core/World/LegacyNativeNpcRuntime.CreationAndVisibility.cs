using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Genuine native Npc physical owner/prerequisites, NOT IMapObj adoption.
// Complete GMap/Send/Procesor remain held on native map-loader/tournament graph.
internal partial class LegacyNativeNpcRuntime
{
	public bool IsTrap()
	{
		return false;
	}

	public static LegacyNativeNpcRuntime Create()
	{
		LegacyNativeNpcRuntime npc = new LegacyNativeNpcRuntime();
		npc.Leng = 26;
		npc.AllowDynamic = false;
		npc.PacketID = 2210;
		npc.ObjType = LegacyNativeMapObjectType.Npc;
		npc.Alive = true;
		return npc;
	}

	public bool CanSee(ILegacyNativeMapObj obj)
	{
		try
		{
			if (obj == null)
			{
				return false;
			}
			if (obj.Map != Map)
			{
				return false;
			}
			if (!AllowDynamic && obj.DynamicID != DynamicID)
			{
				return false;
			}
			if (obj.UID == UID)
			{
				return false;
			}
			return LegacyNativeRoleCore.GetDistance(obj.X, obj.Y, X, Y) <= 19;
		}
		catch (Exception e)
		{
			LegacyNativeConsole.WriteException(e);
			return false;
		}
	}

	public LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st)
	{
		UnKnow = UID;
		return stream = stream.NpcCreate(this, 0);
	}
}
