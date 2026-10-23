namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Scoped compatibility for the two native Aura uint-cast expression families only.
    // Root's OWNED Framework4.0.30319.42000 x86 probe passed66 controls under csc
    // default and /optimize+: .artifacts/native-aura-conversion-probe/Program.cs.
    // Native x87 retains a widened scaled product, then truncates to signed64 and
    // takes low32. Modern direct float->uint saturates and rounds the product early.
    private static uint NativeAuraUInt32(double value)
    {
        return value >= -9223372036854775808.0 && value < 9223372036854775808.0
            ? unchecked((uint)(long)value)
            : 0u;
    }
}
