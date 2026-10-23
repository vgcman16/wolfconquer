using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNpcServer
{
	public static void LoadServerTraps(uint id = 0u)
	{
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "Traps.txt"))
		{
			return;
		}
		using StreamReader streamReader = File.OpenText(LegacyNativeServerConfig.DbLocation + "Traps.txt");
		while (true)
		{
			string? text = streamReader.ReadLine();
			if (text == null || !(text != ""))
			{
				break;
			}
			string[] array = text.Split(',');
			uint num = uint.Parse(array[3]);
			ushort num2 = ushort.Parse(array[0]);
			if ((id == 0 || id == num2) && (num != 1063 || (num2 != 1002 && num2 != 1010 && num2 != 1011 && num2 != 1020 && num2 != 1000 && num2 != 1015)))
			{
				LegacyNativeFloorItemRuntime msgItem = new LegacyNativeFloorItemRuntime(null, ushort.Parse(array[1]), ushort.Parse(array[2]), LegacyNativeFloorItemRuntime.ItemType.Effect, 0u, 0u, ushort.Parse(array[0]), 0u, _ToMySelf: false, LegacyNativePoolMaps.ServerMaps![ushort.Parse(array[0])], 3600000);
				msgItem.MsgFloor.m_ID = num;
				msgItem.MsgFloor.m_Color = 2;
				msgItem.MsgFloor.DropType = LegacyNativeMsgDropID.Effect;
				if (array.Length > 4)
				{
					msgItem.AllowDynamic = byte.Parse(array[4]) == 1;
				}
				msgItem.GMap.View!.EnterMap((ILegacyNativeMapObj)msgItem);
			}
		}
	}
}
