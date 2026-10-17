namespace WolfConquer.Core.World;

public sealed class AnchoredRuntimeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _anchorUtc;
    private readonly TimeProvider _inner;
    private readonly object _gate = new();
    private long? _startTimestamp;

    public AnchoredRuntimeTimeProvider(DateTimeOffset anchorUtc, TimeProvider? inner = null)
    {
        _anchorUtc = anchorUtc.ToUniversalTime();
        _inner = inner ?? TimeProvider.System;
    }

    public override TimeZoneInfo LocalTimeZone => _inner.LocalTimeZone;

    public override long TimestampFrequency => _inner.TimestampFrequency;

    public override DateTimeOffset GetUtcNow()
    {
        var startTimestamp = EnsureStartTimestamp();
        var elapsed = _inner.GetElapsedTime(startTimestamp, _inner.GetTimestamp());
        return _anchorUtc + elapsed;
    }

    public override long GetTimestamp()
    {
        return _inner.GetTimestamp();
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        return _inner.CreateTimer(callback, state, dueTime, period);
    }

    private long EnsureStartTimestamp()
    {
        if (_startTimestamp is { } existing)
        {
            return existing;
        }

        lock (_gate)
        {
            if (_startTimestamp is { } lockedExisting)
            {
                return lockedExisting;
            }

            var timestamp = _inner.GetTimestamp();
            _startTimestamp = timestamp;
            return timestamp;
        }
    }
}
