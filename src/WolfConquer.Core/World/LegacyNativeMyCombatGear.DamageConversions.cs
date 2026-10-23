namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Only AttackEffect modulo/division families; root-owned Framework/x86 probe:
    // 36 rows/118 controls default + optimized, each 0 failures. Not a general converter.
    private static int NativeGearAttackInt32(double value)
        => value >= -2147483648.0 && value < 2147483648.0 ? unchecked((int)value) : int.MinValue;

    // Only Substitution's direct double -> UInt32 use, from the same scoped probe.
    private static uint NativeGearShieldUInt32(double value)
        => value >= -9223372036854775808.0 && value < 9223372036854775808.0
            ? unchecked((uint)(long)value) : 0u;
}
