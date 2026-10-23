using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
	public static void IncXY(LegacyNativePlayerMapFlags.ConquerAngle Facing, ref ushort x, ref ushort y)
	{
		sbyte b2;
		sbyte b = (b2 = 0);
		switch (Facing)
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
		x = (ushort)(x + b);
		y = (ushort)(y + b2);
	}

public static LegacyNativePlayerMapFlags.ConquerAngle GetAngle(ushort X, ushort Y, ushort X2, ushort Y2)
	{
		double num = 0.0;
		double x = X2 - X;
		double y = Y2 - Y;
		double num2 = Math.Atan2(y, x);
		if (num2 < 0.0)
		{
			num2 += Math.PI * 2.0;
		}
		num = 360.0 - num2 * 180.0 / Math.PI;
		// Exact Core expression; method-scoped Framework/x86 bounded byte conversion.
		byte b = unchecked((byte)(int)(7.0 - Math.Floor(num) / 45.0 % 8.0 - 1.0));
		return (LegacyNativePlayerMapFlags.ConquerAngle)(b % 8);
	}
}
