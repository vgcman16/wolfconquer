using System;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
    // Complete native Core54..87. uint study-luck expressions bind to this
    // double overload, NOT the separately existing int-percent overload.
	public static bool Rate(double percent)
	{
		ushort num = 0;
		if (percent == 0.0)
		{
			return false;
		}
		while (NativeRatePercentToInt32(percent) > 0)
		{
			num++;
			percent /= 10.0;
			if (num > 300)
			{
				Console.WriteLine("Problem While in Kernel=======================");
				return true;
			}
		}
		int num2 = 1;
		percent = Math.Round(percent, 4);
		num = 0;
		while (percent != Math.Ceiling(percent))
		{
			percent *= 10.0;
			num2 *= 10;
			percent = Math.Round(percent, 4);
			num++;
			if (num > 300)
			{
				Console.WriteLine("Problem While in Kernel 2  ===============");
				return true;
			}
		}
		return Rate(NativeRatePercentToInt32(percent), num2);
	}

    // Parent-owned Framework4/x86 probe: .artifacts/chi-native-conversion-probe.
    // Native conv.i4 returns Int32.MinValue for NaN/infinity/out-of-range, while
    // modern casts may saturate. Preserve that conversion at ONLY this native
    // Rate(double) overload's two cast sites; do not alter its loops or RNG.
    private static int NativeRatePercentToInt32(double value)
    {
        return value >= int.MinValue && value < 2147483648.0
            ? unchecked((int)value)
            : int.MinValue;
    }
}
