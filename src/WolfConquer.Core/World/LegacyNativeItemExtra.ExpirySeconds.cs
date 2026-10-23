namespace WolfConquer.Core.World;

internal partial class LegacyNativeItemExtra
{
    // Scoped to the two native item-extra expiry expressions. Framework/x86
    // conv.i4 expires out-of-range durations; modern saturation would retain them.
    // Tick multiplication also preserves the native absolute TotalSeconds shape.
    // The full getters keep their real clock, date faults and Available mutation.
    private static int NativeExpirySecondsToInt32(double value)
    {
        return value >= int.MinValue && value < 2147483648.0
            ? unchecked((int)value)
            : int.MinValue;
    }
}
