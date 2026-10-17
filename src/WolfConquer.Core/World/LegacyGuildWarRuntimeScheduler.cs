namespace WolfConquer.Core.World;

public sealed class LegacyGuildWarRuntimeScheduler
{
    public static readonly TimeSpan StartTimeOfDay = new(21, 0, 5);
    public static readonly TimeSpan ActiveDuration = TimeSpan.FromHours(1);

    private readonly LegacyGuildWarRuntimeStateStore _states;
    private readonly object _gate = new();
    private DateOnly? _lastStartedDate;

    public LegacyGuildWarRuntimeScheduler(LegacyGuildWarRuntimeStateStore states)
    {
        _states = states ?? throw new ArgumentNullException(nameof(states));
    }

    public LegacyGuildWarRuntimeTransition Tick(
        DateTimeOffset now,
        TimeZoneInfo? localTimeZone = null)
    {
        var zone = localTimeZone ?? TimeZoneInfo.Local;
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        lock (_gate)
        {
            if (_states.IsActive && !IsInActiveWindow(localNow))
            {
                _states.Stop();
                return LegacyGuildWarRuntimeTransition.Stopped;
            }

            if (!_states.IsActive &&
                IsInActiveWindow(localNow) &&
                _lastStartedDate != localDate)
            {
                _states.Start();
                _lastStartedDate = localDate;
                return LegacyGuildWarRuntimeTransition.Started;
            }
        }

        return LegacyGuildWarRuntimeTransition.None;
    }

    private static bool IsInActiveWindow(DateTimeOffset localNow)
    {
        if (localNow.DayOfWeek != DayOfWeek.Friday)
        {
            return false;
        }

        var timeOfDay = localNow.TimeOfDay;
        return timeOfDay >= StartTimeOfDay &&
            timeOfDay < StartTimeOfDay + ActiveDuration;
    }
}

public enum LegacyGuildWarRuntimeTransition
{
    None = 0,
    Started = 1,
    Stopped = 2,
}
