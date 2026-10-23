using System.Collections.Concurrent;
using System.Drawing;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeGameMap
{
	public Tuple<ushort, ushort> RandomCoordinates()
	{
		int num = 10000;
		int num2 = LegacyNativePoolRandom.GetRandom.Next(bounds.Width);
		int num3 = LegacyNativePoolRandom.GetRandom.Next(bounds.Height);
		while (num-- > 0 && (cells[num2, num3] & LegacyNativeMapFlagType.Valid) != LegacyNativeMapFlagType.Valid)
		{
			num2 = LegacyNativePoolRandom.GetRandom.Next(bounds.Width);
			num3 = LegacyNativePoolRandom.GetRandom.Next(bounds.Height);
		}
		return new Tuple<ushort, ushort>((ushort)num2, (ushort)num3);
	}

	public void GetRandCoord(ref ushort x, ref ushort y)
	{
		lock (SyncRoot)
		{
			do
			{
				x = (ushort)LegacyNativePoolRandom.GetRandom.Next(20, (ushort)(bounds.Width - 1));
				y = (ushort)LegacyNativePoolRandom.GetRandom.Next(20, (ushort)(bounds.Height - 1));
			}
			while ((cells[x, y] & LegacyNativeMapFlagType.Valid) != LegacyNativeMapFlagType.Valid);
		}
	}

	public void GetRandCoord(ref ushort _x, ref ushort _y, int radius)
	{
		int num = 10000;
		lock (SyncRoot)
		{
			_x = (ushort)LegacyNativePoolRandom.GetRandom.Next(_x - radius, _x + radius);
			_y = (ushort)LegacyNativePoolRandom.GetRandom.Next(_y - radius, _y + radius);
			while (num-- > 0 && (cells[_x, _y] & LegacyNativeMapFlagType.Valid) != LegacyNativeMapFlagType.Valid)
			{
				_x = (ushort)LegacyNativePoolRandom.GetRandom.Next(_x + radius * -1, _x + radius);
				_y = (ushort)LegacyNativePoolRandom.GetRandom.Next(_y + radius * -1, _y + radius);
			}
		}
	}

	public void Pushback(ref uint x, ref uint y, LegacyNativePlayerMapFlags.ConquerAngle angle, int paces)
	{
		sbyte b = 0;
		sbyte b2 = 0;
		for (int i = 0; i < paces; i++)
		{
			switch (angle)
			{
			case LegacyNativePlayerMapFlags.ConquerAngle.North:
				b = -1;
				b2 = -1;
				break;
			case LegacyNativePlayerMapFlags.ConquerAngle.South:
				b = 1;
				b2 = 1;
				break;
			case LegacyNativePlayerMapFlags.ConquerAngle.East:
				b = 1;
				b2 = -1;
				break;
			case LegacyNativePlayerMapFlags.ConquerAngle.West:
				b = -1;
				b2 = 1;
				break;
			case LegacyNativePlayerMapFlags.ConquerAngle.NorthWest:
				b = -1;
				break;
			case LegacyNativePlayerMapFlags.ConquerAngle.SouthWest:
				b2 = 1;
				break;
			case LegacyNativePlayerMapFlags.ConquerAngle.NorthEast:
				b2 = -1;
				break;
			case LegacyNativePlayerMapFlags.ConquerAngle.SouthEast:
				b = 1;
				break;
			}
			if (!ValidLocation((ushort)(x + b), (ushort)(y + b2)))
			{
				break;
			}
			x = (ushort)(x + b);
			y = (ushort)(y + b2);
		}
	}
}
