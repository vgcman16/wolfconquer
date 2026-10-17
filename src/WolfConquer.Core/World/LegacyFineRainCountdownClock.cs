namespace WolfConquer.Core.World;

internal static class LegacyFineRainCountdownClock
{
    public static uint Read(
        ILegacySignedMillisecondClock nativeClock,
        ILegacySignedMillisecondClock environmentClock,
        TimeProvider provider,
        DateTime timerLocal,
        int seconds)
    {
        // Preserve operand/read order and signed division toward zero.
        var nativeSeconds = nativeClock.GetMilliseconds() / 1_000;
        var expirationLocal = timerLocal.AddSeconds(seconds);
        var environment = environmentClock.GetMilliseconds();
        var nowLocal = LegacyFineRain212Invocation.LocalTime(provider, provider.GetUtcNow());
        var delta = unchecked((int)((expirationLocal.Ticks - nowLocal.Ticks) / TimeSpan.TicksPerMillisecond));
        var expirationSeconds = unchecked(environment + delta) / 1_000;
        return unchecked((uint)(nativeSeconds - expirationSeconds));
    }
}
