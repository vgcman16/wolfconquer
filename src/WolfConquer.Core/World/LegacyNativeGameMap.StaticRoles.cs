using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public bool AddStaticRole(LegacyNativeStaticRoleRuntime role)
	{
		if (View!.EnterMap(role))
		{
			SetFlagNpc(role.X, role.Y);
			return true;
		}
		return false;
	}

public bool RemoveStaticRole(ILegacyNativeMapObj obj)
	{
		if (View!.LeaveMap(obj))
		{
			RemoveFlagNpc(obj!.X, obj.Y);
			return true;
		}
		return false;
	}
}
