using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

public static class LegacySignedMillisecondClock
{
    public static ILegacySignedMillisecondClock System { get; } = new SystemClock();

    public static ILegacySignedMillisecondClock FromTimeProvider(
        TimeProvider timeProvider,
        int epochMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return new TimeProviderClock(timeProvider, epochMilliseconds);
    }

    private sealed class SystemClock : ILegacySignedMillisecondClock
    {
        public int GetMilliseconds()
        {
            if (OperatingSystem.IsWindows())
            {
                return unchecked((int)NativeTimeGetTime());
            }

            // Boot-millisecond approximation only; not exact native Time32 on non-Windows.
            return unchecked((int)Environment.TickCount64);
        }
    }

    private sealed class TimeProviderClock : ILegacySignedMillisecondClock
    {
        private readonly TimeProvider _timeProvider;
        private readonly int _epochMilliseconds;
        private readonly long _originTimestamp;

        public TimeProviderClock(TimeProvider timeProvider, int epochMilliseconds)
        {
            _timeProvider = timeProvider;
            _epochMilliseconds = epochMilliseconds;
            _originTimestamp = timeProvider.GetTimestamp();
        }

        public int GetMilliseconds()
        {
            var elapsedMilliseconds = _timeProvider
                .GetElapsedTime(_originTimestamp, _timeProvider.GetTimestamp())
                .Ticks / TimeSpan.TicksPerMillisecond;
            return unchecked(_epochMilliseconds + (int)elapsedMilliseconds);
        }
    }

    [DllImport("winmm.dll", EntryPoint = "timeGetTime", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint NativeTimeGetTime();
}
