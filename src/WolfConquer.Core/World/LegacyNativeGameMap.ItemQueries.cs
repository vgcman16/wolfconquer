using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public int ContainItem(uint Look)
	{
		int num = 0;
		foreach (ILegacyNativeMapObj allMapRole in View!.GetAllMapRoles(LegacyNativeMapObjectType.Item))
		{
			if (allMapRole is LegacyNativeFloorItemRuntime msgItem && msgItem.MsgFloor.m_ID == Look)
			{
				num++;
			}
		}
		return num;
	}

public bool EnqueueItem(LegacyNativeFloorItemRuntime item)
	{
		return View!.EnterMap((ILegacyNativeMapObj)item);
	}

public bool ContainMobID(uint ID, uint Dynamic = 0u, int count = 1)
	{
		foreach (ILegacyNativeMapObj allMapRole in View!.GetAllMapRoles(LegacyNativeMapObjectType.Monster))
		{
			LegacyNativeMonsterRole monsterRole = (allMapRole as LegacyNativeMonsterRole)!;
			if (monsterRole.Family != null && monsterRole.Family.ID == ID && monsterRole.Alive)
			{
				if (Dynamic == 0)
				{
					count--;
				}
				else if (Dynamic == allMapRole.DynamicID)
				{
					count--;
				}
			}
		}
		return count <= 0;
	}
}
