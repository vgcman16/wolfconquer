namespace WolfConquer.Core.World;

// Complete preserved SystemExtensions System.Time32 physical state. This is a
// signed 32-bit tick value, not a DateTime/Environment.TickCount approximation.
internal partial struct LegacyNativeTime32
{
    private int int_0;

    private static uint uint_0;

    // Inline birth preserves the original beforefieldinit type initializer.
    public static readonly LegacyNativeTime32 NULL = new LegacyNativeTime32(0);

    public int TotalMilliseconds => int_0;

    public int Value => int_0;

    public LegacyNativeTime32(int Value)
    {
        int_0 = Value;
    }

    public LegacyNativeTime32(uint Value)
    {
        int_0 = unchecked((int)Value);
    }

    public LegacyNativeTime32(long Value)
    {
        int_0 = unchecked((int)Value);
    }
}
