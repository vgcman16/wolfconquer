using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeConfiscatorRuntime
{
internal static int CreateTimer(int year, int month, int day)
	{
		return year * 10000 + month * 100 + day;
	}

internal static DateTime GetTimer(int Timer)
	{
		int num = Timer / 10000;
		int num2 = Timer / 100 - num * 100;
		int day = Timer - num * 10000 - num2 * 100;
		return new DateTime(num, num2, day);
	}

internal static int CalculateCpsCost(LegacyNativeMsgDetainedItem Item)
	{
		int num = 10;
		if (Item.ItemID % 10 == 9)
		{
			num += 40;
		}
		switch (Item.Plus)
		{
		case 1:
			num++;
			break;
		case 2:
			num += 2;
			break;
		case 3:
			num += 5;
			break;
		case 4:
			num += 15;
			break;
		case 5:
			num += 30;
			break;
		case 6:
			num += 90;
			break;
		case 7:
			num += 270;
			break;
		case 8:
			num += 600;
			break;
		default:
			num += 2000000;
			break;
		case 0:
			break;
		}
		ushort num2 = LegacyNativeItemPosition.ItemPosition(Item.ItemID);
		ushort num3 = num2;
		if ((uint)(num3 - 4) <= 1u || (uint)(num3 - 54) <= 1u)
		{
			if (Item.SocketOne != Flags.Gem.NoSocket && Item.SocketTwo != Flags.Gem.NoSocket)
			{
				num += 1000000;
			}
			else if (Item.SocketOne != Flags.Gem.NoSocket)
			{
				num += 10;
			}
		}
		else if (Item.SocketOne != Flags.Gem.NoSocket && Item.SocketTwo != Flags.Gem.NoSocket)
		{
			num += 1000000;
		}
		else if (Item.SocketOne != Flags.Gem.NoSocket)
		{
			num += 1000000;
		}
		switch (Item.Refinary.EffectLevel)
		{
		case 1u:
			num += 30;
			break;
		case 2u:
			num += 90;
			break;
		case 3u:
			num += 200;
			break;
		case 4u:
			num += 400;
			break;
		case 5u:
			num += 600;
			break;
		case 6u:
			num += 1000000;
			break;
		case 7u:
			num += 1000000;
			break;
		}
		switch (Item.Purification.PurificationLevel)
		{
		case 1u:
			num += 30;
			break;
		case 2u:
			num += 90;
			break;
		case 3u:
			num += 180;
			break;
		case 4u:
			num += 300;
			break;
		case 5u:
			num += 450;
			break;
		case 6u:
			num += 1000000;
			break;
		case 7u:
			num += 1000000;
			break;
		}
		return num;
	}
}
