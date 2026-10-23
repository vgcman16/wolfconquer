namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    // Production passes one local DateTime.Now sample. This is a local-calendar
    // countdown, not an elapsed/monotonic timer or a claim that the scheduler ran.
    internal static uint RemainingUntilNextDailyReset(DateTime now)
    {
        long ticks = TimeSpan.TicksPerDay - now.TimeOfDay.Ticks;
        // A fractional remaining second must not display zero before midnight.
        // The complete range is1..86400; integer arithmetic never wraps a UInt32.
        return (uint)((ticks + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond);
    }
}
