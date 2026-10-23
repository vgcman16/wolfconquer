using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public void GenerateSectorTraps(ushort x, ushort y, int type)
	{
		if (View!.CountRoles(LegacyNativeMapObjectType.Item, x, y) >= 6)
		{
			return;
		}
		ushort num = (ushort)LegacyNativePoolRandom.GetRandom.Next(1, 18);
		ushort num2 = (ushort)LegacyNativePoolRandom.GetRandom.Next(1, 18);
		num += x;
		num2 += y;
		if (!IsFlagPresent(num, num2, LegacyNativeMapFlagType.Item) && IsFlagPresent(num, num2, LegacyNativeMapFlagType.Valid))
		{
			LegacyNativeFloorItemRuntime msgItem = new LegacyNativeFloorItemRuntime(null, num, num2, LegacyNativeFloorItemRuntime.ItemType.Effect, 0u, 0u, ID, 0u, _ToMySelf: false, this, 3600000);
			msgItem.MsgFloor.m_ID = (uint)type;
			msgItem.MsgFloor.m_Color = 2;
			msgItem.MsgFloor.DropType = LegacyNativeMsgDropID.Effect;
			cells[num, num2] |= LegacyNativeMapFlagType.Item;
			View!.EnterMap((ILegacyNativeMapObj)msgItem);
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			msgItem.SendAll(stream, LegacyNativeMsgDropID.Effect);
		}
	}

public void RemoveTrap(ushort x, ushort y, ILegacyNativeMapObj item)
	{
		View!.LeaveMap(item);
		cells[item.X, item.Y] &= ~LegacyNativeMapFlagType.Item;
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeFloorItemRuntime msgItem = (item as LegacyNativeFloorItemRuntime)!;
		msgItem.SendAll(stream, LegacyNativeMsgDropID.RemoveEffect);
	}
}
