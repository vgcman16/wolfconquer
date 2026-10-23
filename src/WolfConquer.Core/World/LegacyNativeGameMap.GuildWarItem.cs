using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public void GuildWarItem(int type)
	{
		Tuple<ushort, ushort> tuple = RandomCoordinates();
		ushort item = tuple.Item1;
		ushort item2 = tuple.Item2;
		if (!IsFlagPresent(item, item2, LegacyNativeMapFlagType.Item) && IsFlagPresent(item, item2, LegacyNativeMapFlagType.Valid))
		{
			LegacyNativeFloorItemRuntime msgItem = new LegacyNativeFloorItemRuntime(null, item, item2, LegacyNativeFloorItemRuntime.ItemType.Effect, 0u, 0u, ID, 0u, _ToMySelf: false, this, 3600000);
			msgItem.MsgFloor.m_ID = (uint)type;
			msgItem.MsgFloor.m_Color = 1;
			msgItem.MsgFloor.Name = "STR_TRAP_ID_" + type + "@@";
			msgItem.MsgFloor.DropType = LegacyNativeMsgDropID.Effect;
			cells[item, item2] |= LegacyNativeMapFlagType.Item;
			View!.EnterMap((ILegacyNativeMapObj)msgItem);
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			msgItem.SendAll(stream, LegacyNativeMsgDropID.Effect);
		}
	}
}
