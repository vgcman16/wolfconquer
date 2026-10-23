using System.Linq;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
    // Whole native ItemType448..872. All54 separate perfection comparisons retained.
	public static uint GetItemPoints(LegacyNativeItemRuneMetadata _dbitem, LegacyNativeGameItemRuntime item)
	{
		uint num = ((item.GetPerfectionPosition != 9) ? 50u : 0u);
		num = ((_dbitem.Level <= 120) ? (num + (uint)((double)(int)_dbitem.Level / 10.0 * 30.0)) : ((_dbitem.Level <= 120 || _dbitem.Level > 130) ? (num + (uint)((double)(int)_dbitem.Level / 10.0 * 60.0)) : (num + (uint)((double)(int)_dbitem.Level / 10.0 * 50.0))));
		if (LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != 9 && LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != 17 && LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != 16 && LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != 15 && LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != 13)
		{
			switch (_dbitem.ID % 10)
			{
			case 6u:
				num += 50;
				break;
			case 7u:
				num += 150;
				break;
			case 8u:
				num += 250;
				break;
			case 9u:
				num += 450;
				break;
			}
		}
		else
		{
			num = 0u;
		}
		switch (item.Bless)
		{
		case 1:
			num += 100;
			break;
		case 3:
			num += 300;
			break;
		case 5:
			num += 500;
			break;
		case 7:
			num += 700;
			break;
		}
		if (item.SocketOne != Flags.Gem.NoSocket)
		{
			num += 1000;
			switch ((int)item.SocketOne % 10)
			{
			case 1:
				num += 200;
				break;
			case 2:
				num += 500;
				break;
			case 3:
				num += 800;
				break;
			}
		}
		if (item.SocketTwo != Flags.Gem.NoSocket)
		{
			num += 2500;
			switch ((int)item.SocketTwo % 10)
			{
			case 1:
				num += 200;
				break;
			case 2:
				num += 500;
				break;
			case 3:
				num += 800;
				break;
			}
		}
		switch (item.Plus)
		{
		case 1:
			num += 200;
			break;
		case 2:
			num += 600;
			break;
		case 3:
			num += 1200;
			break;
		case 4:
			num += 1800;
			break;
		case 5:
			num += 2600;
			break;
		case 6:
			num += 3500;
			break;
		case 7:
			num += 4800;
			break;
		case 8:
			num += 5800;
			break;
		case 9:
			num += 6800;
			break;
		case 10:
			num += 7800;
			break;
		case 11:
			num += 8800;
			break;
		case 12:
			num += 10000;
			break;
		}
		switch (item.Purification.PurificationLevel)
		{
		case 7u:
			num += 2000;
			break;
		case 6u:
			num += 1600;
			break;
		case 5u:
			num += 1200;
			break;
		case 4u:
			num = ((LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) != 6) ? (num + 800) : (num + 2000));
			break;
		case 3u:
			num += 500;
			break;
		}
		switch (item.Refinary.EffectLevel)
		{
		case 6u:
			num += 2000;
			break;
		case 5u:
			num += 1600;
			break;
		case 4u:
			num += 1200;
			break;
		case 3u:
			num += 800;
			break;
		case 2u:
			num += 200;
			break;
		case 1u:
			num += 100;
			break;
		}
		if (item.GetPerfectionPosition != 9)
		{
			num += (uint)(item.Enchant * 2);
		}
		if (item.PerfectionLevel >= 1)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 2)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 3)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 4)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 5)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 6)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 7)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 8)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 9)
		{
			num += 180;
		}
		if (item.PerfectionLevel >= 10)
		{
			num += 2380;
		}
		if (item.PerfectionLevel >= 11)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 12)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 13)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 14)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 15)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 16)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 17)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 18)
		{
			num += 400;
		}
		if (item.PerfectionLevel >= 19)
		{
			num += 5150;
		}
		if (item.PerfectionLevel >= 20)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 21)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 22)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 23)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 24)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 25)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 26)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 27)
		{
			num += 650;
		}
		if (item.PerfectionLevel >= 28)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 29)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 30)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 31)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 32)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 33)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 34)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 35)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 36)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 37)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 38)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 39)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 40)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 41)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 42)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 43)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 44)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 45)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 46)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 47)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 48)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 49)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 50)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 51)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 52)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 53)
		{
			num += 100;
		}
		if (item.PerfectionLevel >= 54)
		{
			num += 100;
		}
		if (LegacyNativeItemPosition.ItemPosition(item.ITEM_ID) == 13)
		{
			foreach (LegacyNativeRelicAttribute item2 in item.RelicAttributes.Where((LegacyNativeRelicAttribute i) => (uint)i != 0))
			{
				switch (item2.Type)
				{
				case LegacyNativeChiAttribute.CriticalStrike:
					num += (uint)(4 * item2.Value);
					break;
				case LegacyNativeChiAttribute.SkillCriticalStrike:
					num += (uint)(4 * item2.Value);
					break;
				case LegacyNativeChiAttribute.Immunity:
					num += (uint)(4 * item2.Value);
					break;
				case LegacyNativeChiAttribute.Breakthrough:
					num += (uint)(4 * item2.Value);
					break;
				case LegacyNativeChiAttribute.Counteraction:
					num += (uint)(4 * item2.Value);
					break;
				case LegacyNativeChiAttribute.HPAdd:
					num += item2.Value;
					break;
				case LegacyNativeChiAttribute.AddAttack:
					num += (uint)(2 * item2.Value);
					break;
				case LegacyNativeChiAttribute.AddMagicAttack:
					num += item2.Value;
					break;
				case LegacyNativeChiAttribute.AddMagicDefense:
					num += (uint)(5 * item2.Value);
					break;
				case LegacyNativeChiAttribute.PhysicalDamageIncrease:
					num += item2.Value;
					break;
				case LegacyNativeChiAttribute.MagicDamageIncrease:
					num += item2.Value;
					break;
				case LegacyNativeChiAttribute.PhysicalDamageDecrease:
					num += item2.Value;
					break;
				case LegacyNativeChiAttribute.MagicDamageDecrease:
					num += item2.Value;
					break;
				}
			}
		}
		if (item.AnimaItemID != 0)
		{
			num += item.AnimaItemID % 100 * 500;
		}
		return num;
	}
}
