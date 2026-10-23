namespace WolfConquer.Core.World;

// Scoped ONLY to BreathFocus's byte Damage and AddMana/ShieldBlock's scalar uint
// Damage sites. Root-owned Framework4/x86 source-shaped probe: 256 controls,
// 32 scalar rows, zero failures default and /optimize+. No original assembly run.
internal static class LegacyNativeAttackConversions
{
    internal static uint UInt32(double value)
    {
        return value >= -9223372036854775808.0 && value < 9223372036854775808.0
            ? unchecked((uint)(long)value) : 0u;
    }

    internal static byte Byte(float value)
    {
        return unchecked((byte)UInt32(value));
    }
}
