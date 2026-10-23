namespace WolfConquer.Core.World;

// Complete native Calculate.Base method family; no modern combat projection.
internal static partial class LegacyNativeAttackBase
{
	public static double GetDDistance(ushort X, ushort Y, ushort X2, ushort Y2)
	{
		return Math.Sqrt((X - X2) * (X - X2) + (Y - Y2) * (Y - Y2));
	}

	public static short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
	{
		short num = 0;
		short num2 = 0;
		if (X >= X2)
		{
			num = (short)(X - X2);
		}
		else if (X2 >= X)
		{
			num = (short)(X2 - X);
		}
		if (Y >= Y2)
		{
			num2 = (short)(Y - Y2);
		}
		else if (Y2 >= Y)
		{
			num2 = (short)(Y2 - Y);
		}
		if (num > num2)
		{
			return num;
		}
		return num2;
	}
}
