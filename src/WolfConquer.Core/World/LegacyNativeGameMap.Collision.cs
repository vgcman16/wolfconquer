using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public bool ValidLocation(ushort X, ushort Y)
	{
		if (bounds.Width > X && bounds.Height > Y)
		{
			return (cells[X, Y] & LegacyNativeMapFlagType.Valid) == LegacyNativeMapFlagType.Valid || (cells[X, Y] & LegacyNativeMapFlagType.Npc) == LegacyNativeMapFlagType.Npc;
		}
		return false;
	}

	public bool MonsterOnTile(ushort X, ushort Y)
	{
		if (bounds.Width > X && bounds.Height > Y)
		{
			return (cells[X, Y] & LegacyNativeMapFlagType.Monster) == LegacyNativeMapFlagType.Monster;
		}
		return false;
	}

	public void SetMonsterOnTile(ushort X, ushort Y, bool Value)
	{
		try
		{
			if (Value)
			{
				cells[X, Y] |= LegacyNativeMapFlagType.Monster;
			}
			else
			{
				cells[X, Y] &= ~LegacyNativeMapFlagType.Monster;
			}
		}
		catch (Exception e)
		{
			LegacyNativeConsole.WriteException(e);
			LegacyNativeConsole.WriteLine("Problem monsters on map " + ID, ConsoleColor.DarkRed);
		}
	}

	public bool IsFlagPresent(int x, int y, LegacyNativeMapFlagType flag)
	{
		if (x > 0 && y > 0 && x < bounds.Width && y < bounds.Height)
		{
			return (cells[x, y] & flag) == flag;
		}
		return false;
	}

	public void SetFlagNpc(ushort x, ushort y)
	{
		cells[x, y] = LegacyNativeMapFlagType.Npc;
		ushort num = (ushort)Math.Min(bounds.Height - 2, y + 2);
		ushort num2 = (ushort)Math.Min(bounds.Width - 2, x + 2);
		ushort num3 = (ushort)Math.Max(x - 2, 0);
		for (ushort num4 = (ushort)Math.Max(y - 2, 0); num4 <= num; num4++)
		{
			for (ushort num5 = num3; num5 <= num2; num5++)
			{
				cells[num5, num4] = LegacyNativeMapFlagType.Npc;
			}
		}
	}

	public void SetGateFlagNpc(ushort x, ushort y)
	{
		cells[x, y] = LegacyNativeMapFlagType.None;
		ushort num = (ushort)Math.Min(bounds.Height - 2, y + 2);
		ushort num2 = (ushort)Math.Min(bounds.Width - 2, x + 2);
		ushort num3 = (ushort)Math.Max(x - 2, 0);
		for (ushort num4 = (ushort)Math.Max(y - 2, 0); num4 <= num; num4++)
		{
			for (ushort num5 = num3; num5 <= num2; num5++)
			{
				cells[num5, num4] = LegacyNativeMapFlagType.None;
			}
		}
	}

	public void RemoveFlagNpc(ushort x, ushort y)
	{
		cells[x, y] = LegacyNativeMapFlagType.Valid;
		ushort num = (ushort)Math.Min(bounds.Height - 1, y + 1);
		ushort num2 = (ushort)Math.Min(bounds.Width - 1, x + 1);
		ushort num3 = (ushort)Math.Max(x - 1, 0);
		for (ushort num4 = (ushort)Math.Max(y - 1, 0); num4 <= num; num4++)
		{
			for (ushort num5 = num3; num5 <= num2; num5++)
			{
				cells[num5, num4] = LegacyNativeMapFlagType.Valid;
			}
		}
	}
}
