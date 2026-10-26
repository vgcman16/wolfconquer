namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static long ResolveLegacyAccountManagerFullPromotionClass(long classCode)
    {
        return classCode switch
        {
            >= 1_000 and <= 1_051 => 1_051,
            >= 2_000 and <= 2_051 => 2_051,
            >= 4_000 and <= 4_051 => 4_051,
            >= 5_000 and <= 5_051 => 5_051,
            >= 6_000 and <= 6_051 => 6_051,
            >= 7_000 and <= 7_051 => 7_051,
            >= 8_000 and <= 8_051 => 8_051,
            >= 9_000 and <= 9_051 => 9_051,
            >= 13_000 and <= 13_051 => 13_051,
            >= 14_000 and <= 14_051 => 14_051,
            >= 16_000 and <= 16_051 => 16_051,
            _ => classCode,
        };
    }
}
