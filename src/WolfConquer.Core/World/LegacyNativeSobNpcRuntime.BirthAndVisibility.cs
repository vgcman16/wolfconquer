using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSobNpcRuntime
{
	public LegacyNativeSobNpcRuntime(LegacyNativeStatue _statue)
	{
		statue = _statue;
		BitVector = new LegacyNativeStatusFlagsBigVector32(576);
	}

public bool CanSee(ILegacyNativeMapObj obj)
	{
		if (DynamicID != obj.DynamicID)
		{
			return false;
		}
		if (Map != obj.Map)
		{
			return false;
		}
		if (LegacyNativeRoleCore.GetDistance(X, Y, obj.X, obj.Y) >= 18)
		{
			return false;
		}
		return true;
	}

public void SendView(LegacyNativePacket msg, LegacyNativeGameMap GMap)
	{
		foreach (ILegacyNativeMapObj item in GMap.View!.Roles(LegacyNativeMapObjectType.Player, X, Y, (ILegacyNativeMapObj p) => CanSee(p)))
		{
			item.Send(msg);
		}
	}

public LegacyNativeSobNpcRuntime()
	{
		AllowDynamic = false;
		BitVector = new LegacyNativeStatusFlagsBigVector32(576);
	}

public bool IsTrap()
	{
		return false;
	}

public void Send(LegacyNativePacket msg)
	{
	}
}
