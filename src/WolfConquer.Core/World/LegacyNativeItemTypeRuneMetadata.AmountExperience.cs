namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
	public static uint MoneyItemID(uint value)
	{
		if (value < 100)
		{
			return 1090000u;
		}
		if (value < 399)
		{
			return 1090010u;
		}
		if (value < 5099)
		{
			return 1090020u;
		}
		if (value < 8099)
		{
			return 1091000u;
		}
		if (value < 12099)
		{
			return 1091010u;
		}
		return 1091020u;
	}

	public static ulong CalculateExpBall(byte Level)
	{
		if (Level < 130)
		{
			return (ulong)(680462.7536 + 3479.5308 * (double)(Level / 2) * (double)(int)Level);
		}
		if (Level < 135)
		{
			return (ulong)((680462.7536 + 3479.5308 * (double)(Level / 2) * (double)(int)Level) * (double)(Level % 10 + 6));
		}
		return (ulong)((680462.7536 + 3479.5308 * (double)(Level / 2) * (double)(int)Level) * (double)(Level % 10 + 8));
	}
}
