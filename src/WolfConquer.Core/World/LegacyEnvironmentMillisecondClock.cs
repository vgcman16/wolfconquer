namespace WolfConquer.Core.World;

// SharedExtensions.DateTime.AllMilliseconds uses Environment.TickCount, not Time32.Now.
public static class LegacyEnvironmentMillisecondClock
{
    public static ILegacySignedMillisecondClock System { get; } = new EnvironmentClock();

    public static ILegacySignedMillisecondClock FromTimeProvider(TimeProvider provider, int epochMilliseconds) =>
        LegacySignedMillisecondClock.FromTimeProvider(provider, epochMilliseconds);

    private sealed class EnvironmentClock : ILegacySignedMillisecondClock
    {
        public int GetMilliseconds() => Environment.TickCount;
    }
}
