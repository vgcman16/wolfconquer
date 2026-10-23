namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCalculatePhysical
{
    // Only OnMonster's native float factor. Source-shaped owned Framework/x86 v2:
    // 1193 controls/0 failures default and optimized; digest4121549926. No original assembly.
    private static int PhysicalMonsterFactor(float value)
        => value >= -2147483648.0f && value < 2147483648.0f ? unchecked((int)value) : int.MinValue;

    // Only OnMonster's uint damage × uint power /100. Keep floating source order.
    // Exact ulong multiplication was rejected by the owned v1 probe (780/1193 failures).
    private static uint PhysicalMonsterScale(uint damage, uint power)
    {
        double value = (double)damage * (double)power / 100.0;
        return value >= -9223372036854775808.0 && value < 9223372036854775808.0
            ? unchecked((uint)(long)value) : 0u;
    }
}
