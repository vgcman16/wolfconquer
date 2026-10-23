using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public bool IsValidFlagNpc(ushort x, ushort y)
	{
		ushort num = (ushort)Math.Min(bounds.Height - 1, y + 1);
		ushort num2 = (ushort)Math.Min(bounds.Width - 1, x + 1);
		ushort num3 = (ushort)Math.Max(x - 1, 0);
		for (ushort num4 = (ushort)Math.Max(y - 1, 0); num4 <= num; num4++)
		{
			for (ushort num5 = num3; num5 <= num2; num5++)
			{
				if (!IsFlagPresent(x, y, LegacyNativeMapFlagType.Valid))
				{
					return false;
				}
			}
		}
		return true;
	}

	public bool AddGuildTeleporterItem(ref ushort x, ref ushort y)
	{
		if (IsValidFlagNpc(x, y))
		{
			ushort num = (ushort)Math.Min(bounds.Height - 6, y + 6);
			ushort num2 = (ushort)Math.Min(bounds.Width - 6, x + 6);
			ushort num3 = (ushort)Math.Max(x - 6, 0);
			ushort num4 = (ushort)Math.Max(y - 6, 0);
			for (ushort num5 = num4; num5 <= num; num5++)
			{
				for (ushort num6 = num3; num6 <= num2; num6++)
				{
					if (IsValidFlagNpc(num6, num5))
					{
						x = num6;
						y = num5;
						cells[num6, num5] |= LegacyNativeMapFlagType.Item;
						return true;
					}
				}
			}
			x = 0;
			y = 0;
			return false;
		}
		cells[x, y] |= LegacyNativeMapFlagType.Item;
		return true;
	}

	public bool AddGroundItem(ref ushort x, ref ushort y, byte Range = 0)
	{
		if (IsFlagPresent(x, y, LegacyNativeMapFlagType.Item) || !IsFlagPresent(x, y, LegacyNativeMapFlagType.Valid))
		{
			ushort num = (ushort)Math.Min(bounds.Height - (1 + Range), y + (1 + Range));
			ushort num2 = (ushort)Math.Min(bounds.Width - (1 + Range), x + (1 + Range));
			ushort num3 = (ushort)Math.Max(x - (1 + Range), 0);
			ushort num4 = (ushort)Math.Max(y - (1 + Range), 0);
			for (ushort num5 = num4; num5 <= num; num5++)
			{
				for (ushort num6 = num3; num6 <= num2; num6++)
				{
					if (!IsFlagPresent(num6, num5, LegacyNativeMapFlagType.Item) && IsFlagPresent(num6, num5, LegacyNativeMapFlagType.Valid))
					{
						x = num6;
						y = num5;
						cells[num6, num5] |= LegacyNativeMapFlagType.Item;
						return true;
					}
				}
			}
			x = 0;
			y = 0;
			return false;
		}
		cells[x, y] |= LegacyNativeMapFlagType.Item;
		return true;
	}
}
