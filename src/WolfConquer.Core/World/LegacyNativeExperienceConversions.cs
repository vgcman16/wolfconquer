namespace WolfConquer.Core.World;

// Scoped original Framework/x86 conv.u8 arithmetic, not a general numeric policy.
// Root independently validated22 bit-pattern and640 source-expression controls
// under original Framework/x86, default and optimized. Not game-executable proof.
internal static class LegacyNativeExperienceConversions
{
    internal static ulong UInt64(double value)
    {
        const double signBoundary = 9223372036854775808.0;
        if (value < signBoundary)
        {
            return unchecked((ulong)Int64(value));
        }
        return unchecked((ulong)Int64(value - signBoundary)) ^ 0x8000000000000000UL;
    }

    private static long Int64(double value)
    {
        if (double.IsNaN(value) || value < -9223372036854775808.0 ||
            value >= 9223372036854775808.0)
        {
            return long.MinValue;
        }
        return (long)value;
    }
}
