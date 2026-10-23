namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Owner-scoped casts: Framework4/x86 default/optimized literal observations
    // are compared through the actual modern A/Status owners, not just this helper.
    private static uint NativeCombatStatusUInt32(double value)
    {
        if (double.IsNaN(value) || value < -9223372036854775808.0 || value >= 9223372036854775808.0) return 0;
        return unchecked((uint)(long)value);
    }

    private static int NativeCombatStatusInt32(double value)
    {
        if (double.IsNaN(value) || value < int.MinValue || value >= 2147483648.0) return int.MinValue;
        return (int)value;
    }
}
